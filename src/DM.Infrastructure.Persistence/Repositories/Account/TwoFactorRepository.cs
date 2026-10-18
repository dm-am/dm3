using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.TwoFactor;
using DM.Domain.Core.Abstractions;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc />
internal class TwoFactorRepository : ITwoFactorRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IGuidFactory _guidFactory;

    public TwoFactorRepository(DmDbContext dbContext, IGuidFactory guidFactory)
    {
        _dbContext = dbContext;
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public Task<bool> IsConfirmed(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.UserTwoFactors
            .AnyAsync(f => f.UserId == userId && f.ConfirmedUtc != null, cancellationToken);

    /// <inheritdoc />
    public Task<TwoFactorState?> Find(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.UserTwoFactors
            .Where(f => f.UserId == userId)
            .Select(f => new TwoFactorState
            {
                UserId = f.UserId,
                Secret = f.Secret,
                CreatedUtc = f.CreatedUtc,
                ConfirmedUtc = f.ConfirmedUtc,
                LastVerifiedUtc = f.LastVerifiedUtc,
                LastAcceptedStep = f.LastAcceptedStep,
                RecoveryCodesIssuedUtc = f.RecoveryCodesIssuedUtc,
                RemovalDueUtc = f.RemovalDueUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<TwoFactorAccount?> FindAccountByEmail(
        string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _dbContext.Users
            .Where(u => !u.IsRemoved && u.Email != null && u.Email.ToLower() == normalized)
            .Select(u => new TwoFactorAccount(u.UserId, u.Username, u.Email, u.Role,
                _dbContext.UserSettings
                    .Where(s => s.UserId == u.UserId)
                    .Select(s => s.TimeZoneId)
                    .FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<TwoFactorAccount?> FindAccountByUsername(
        string username, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        return _dbContext.Users
            .Where(u => !u.IsRemoved && u.Username.ToLower() == normalized)
            .Select(u => new TwoFactorAccount(u.UserId, u.Username, u.Email, u.Role,
                _dbContext.UserSettings
                    .Where(s => s.UserId == u.UserId)
                    .Select(s => s.TimeZoneId)
                    .FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<TwoFactorAccount?> FindAccount(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.Users
            .Where(u => !u.IsRemoved && u.UserId == userId)
            .Select(u => new TwoFactorAccount(u.UserId, u.Username, u.Email, u.Role,
                _dbContext.UserSettings
                    .Where(s => s.UserId == u.UserId)
                    .Select(s => s.TimeZoneId)
                    .FirstOrDefault()))
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> IssueSecret(
        Guid userId, string encryptedSecret, DateTimeOffset createdUtc,
        CancellationToken cancellationToken = default)
    {
        // One statement, so there is no window between the two halves for
        // anything to arrive in. A delete followed by an insert let two setups
        // started at the same moment both delete nothing and then insert the
        // same key, and the one that lost answered its owner with a failure of
        // the storage engine on the screen where they were making the account
        // safer.
        //
        // Only an unconfirmed row is replaced. A confirmed one is refused above
        // this layer as well, and the predicate is repeated in the write because
        // that is the only place it can be checked and acted on at once: zero
        // rows is what the caller turns into the refusal.
        //
        // The replacement leaves the row as clean as the insert does. Every column
        // the update does not name would otherwise survive from the setup this one
        // replaces: a verification stamp from an attempt that was never confirmed,
        // an issue date for a set of recovery codes that belongs to no factor, a
        // removal already scheduled from the mailbox. Before this was a single
        // statement the previous row was deleted, so a new setup began on nothing;
        // it has to keep beginning on nothing.
        var affected = await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UserTwoFactors" ("UserId", "Secret", "CreatedUtc", "LastAcceptedStep")
            VALUES ({userId}, {encryptedSecret}, {createdUtc}, 0)
            ON CONFLICT ("UserId") DO UPDATE SET
                "Secret" = {encryptedSecret},
                "CreatedUtc" = {createdUtc},
                "LastAcceptedStep" = 0,
                "LastVerifiedUtc" = NULL,
                "RecoveryCodesIssuedUtc" = NULL,
                "RemovalDueUtc" = NULL
            WHERE "UserTwoFactors"."ConfirmedUtc" IS NULL
            """, cancellationToken);

        return affected == 1;
    }

    /// <inheritdoc />
    public async Task<bool> Confirm(
        Guid userId, DateTimeOffset createdUtc, DateTimeOffset confirmedUtc,
        IReadOnlyList<byte[]> recoveryCodeHashes,
        CancellationToken cancellationToken = default)
    {
        var rows = RecoveryCodeRows(userId, recoveryCodeHashes, confirmedUtc);

        // Both halves or neither. The set is shown in the answer to the
        // confirmation and never again, so a factor switched on beside a set
        // that failed to store leaves the owner tied to one device with nothing
        // to get past it; the other way round issues codes for a factor that is
        // off. Either the account comes out of this call switched on and holding
        // its set, or it comes out untouched and the person confirms again.
        var switchedOn = false;
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // The issue moment as well as the stamp: the row is replaced in place
            // by a setup started elsewhere, so "still unconfirmed" alone would let
            // this stamp switch the account on to a secret whose QR nobody here
            // ever saw. Written as one predicate rather than a read and a write,
            // because a read of the moment stops being true the instant it returns.
            var updated = await _dbContext.UserTwoFactors
                .Where(f => f.UserId == userId &&
                            f.CreatedUtc == createdUtc &&
                            f.ConfirmedUtc == null)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(f => f.ConfirmedUtc, confirmedUtc), cancellationToken);

            switchedOn = updated == 1;
            if (!switchedOn)
            {
                // The secret this confirmation was checked against is gone -
                // replaced by another setup, or already stamped by a confirmation
                // that arrived first. There is nothing to issue: the set belongs
                // to whichever call switched the factor on, and this caller is
                // refused above.
                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            await ReplaceRecoveryCodesWith(userId, rows, confirmedUtc, cancellationToken);
            await SaveOrClearTracker(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        return switchedOn;
    }

    /// <inheritdoc />
    public async Task<bool> TryAcceptStep(
        Guid userId, long step, DateTimeOffset verifiedUtc,
        CancellationToken cancellationToken = default)
    {
        // "Only if the recorded step is smaller." Two requests carrying one code
        // reach this together and exactly one of them changes a row; the loser
        // sees zero and is refused, which is what makes a time step good for one
        // use across every session and every challenge of the account.
        var updated = await _dbContext.UserTwoFactors
            .Where(f => f.UserId == userId && f.LastAcceptedStep < step)
            .ExecuteUpdateAsync(s => s
                .SetProperty(f => f.LastAcceptedStep, step)
                .SetProperty(f => f.LastVerifiedUtc, verifiedUtc), cancellationToken);
        return updated == 1;
    }

    /// <inheritdoc />
    public Task MarkVerified(
        Guid userId, DateTimeOffset verifiedUtc, CancellationToken cancellationToken = default) =>
        _dbContext.UserTwoFactors
            .Where(f => f.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.LastVerifiedUtc, verifiedUtc), cancellationToken);

    /// <inheritdoc />
    public async Task Remove(Guid userId, CancellationToken cancellationToken = default)
    {
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // The factor row first, the codes after it. Every write that touches
            // both takes them in this order - a removal, a scheduled removal, a
            // reissue - so two of them arriving together queue up instead of each
            // holding the row the other is waiting for.
            await _dbContext.UserTwoFactors
                .Where(f => f.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            // The codes go with the factor. Left behind, they would be accepted
            // by the next factor the account switches on - a set of credentials
            // outliving the thing they belonged to.
            await _dbContext.UserTwoFactorRecoveryCodes
                .Where(c => c.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<bool> RemoveIfDue(
        Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var removed = false;
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // The due date is part of the delete. The sweep listed this account a
            // moment ago, and a cancellation committed since then clears the date:
            // asked as a read, the sweep would take the factor off an owner who
            // had just called the removal off and tell nobody.
            var deleted = await _dbContext.UserTwoFactors
                .Where(f => f.UserId == userId &&
                            f.RemovalDueUtc != null &&
                            f.RemovalDueUtc <= now)
                .ExecuteDeleteAsync(cancellationToken);

            removed = deleted == 1;
            if (removed)
            {
                // Same order as Remove, and the codes only go when the factor did.
                await _dbContext.UserTwoFactorRecoveryCodes
                    .Where(c => c.UserId == userId)
                    .ExecuteDeleteAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        });

        return removed;
    }

    /// <inheritdoc />
    public async Task<bool> ReplaceRecoveryCodes(
        Guid userId, IReadOnlyList<byte[]> codeHashes, DateTimeOffset issuedUtc,
        CancellationToken cancellationToken = default)
    {
        var rows = RecoveryCodeRows(userId, codeHashes, issuedUtc);

        var replaced = false;
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // The factor row is claimed before the codes are touched, which is the
            // order every other write here takes. The replacement below writes the
            // codes first and the row second, so without this a reissue and a
            // removal arriving together would each sit on the row the other needs.
            await _dbContext.Database.ExecuteSqlRawAsync(
                """SELECT "UserId" FROM "UserTwoFactors" WHERE "UserId" = {0} FOR UPDATE""",
                [userId], cancellationToken);

            replaced = await ReplaceRecoveryCodesWith(userId, rows, issuedUtc, cancellationToken) == 1;
            if (!replaced)
            {
                // There is no factor to issue a set for: it was taken off between
                // the caller's read of the state and this write, and the lock above
                // had no row to hold. The new codes are staged in the tracker and
                // the old ones are deleted inside this transaction, so both go back
                // - a set stored against nothing would be accepted by the next
                // factor the account switches on.
                _dbContext.ChangeTracker.Clear();
                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            await SaveOrClearTracker(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        return replaced;
    }

    /// <summary>The rows of one set, identifiers and all.</summary>
    /// <remarks>
    /// Built before the execution strategy runs so that a retry replays the same
    /// identifiers rather than minting a second set of them.
    /// </remarks>
    private List<UserTwoFactorRecoveryCode> RecoveryCodeRows(
        Guid userId, IReadOnlyList<byte[]> codeHashes, DateTimeOffset issuedUtc) =>
        codeHashes
            .Select(hash => new UserTwoFactorRecoveryCode
            {
                RecoveryCodeId = _guidFactory.Create(),
                UserId = userId,
                CodeHash = hash,
                IssuedUtc = issuedUtc
            })
            .ToList();

    /// <summary>
    /// Retire the whole previous set and stage the new one. Runs inside a
    /// transaction the caller owns.
    /// </summary>
    /// <returns>
    /// How many factor rows were dated by this set - one, or none when the
    /// account has no confirmed factor to date.
    /// </returns>
    private async Task<int> ReplaceRecoveryCodesWith(
        Guid userId, IReadOnlyList<UserTwoFactorRecoveryCode> rows, DateTimeOffset issuedUtc,
        CancellationToken cancellationToken)
    {
        // The previous set goes whole. Half old and half new means a code
        // crossed off on paper still opens the account.
        await _dbContext.UserTwoFactorRecoveryCodes
            .Where(c => c.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        _dbContext.UserTwoFactorRecoveryCodes.AddRange(rows);
        // A confirmed factor, not merely a row: a setup that was started and never
        // confirmed is switched off, and a set stored against it would be accepted
        // by whatever secret that row is replaced with next. The confirmation path
        // stamps the moment inside this same transaction, so it sees its own write
        // and passes here.
        return await _dbContext.UserTwoFactors
            .Where(f => f.UserId == userId && f.ConfirmedUtc != null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(f => f.RecoveryCodesIssuedUtc, issuedUtc), cancellationToken);
    }

    /// <summary>
    /// Save, and leave nothing of a failed save behind in the tracker.
    /// </summary>
    /// <remarks>
    /// The context is scoped to the request and the rows above are Added by the
    /// time the save runs, so a failure that is not cleared leaves them staged
    /// for whatever saves next - a retry of the same delegate, or another
    /// repository in the same scope that never asked for them.
    /// </remarks>
    private async Task SaveOrClearTracker(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            _dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(Guid RecoveryCodeId, byte[] CodeHash)>> GetUnusedRecoveryCodes(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.UserTwoFactorRecoveryCodes
            .Where(c => c.UserId == userId && c.UsedUtc == null)
            .Select(c => new { c.RecoveryCodeId, c.CodeHash })
            .ToListAsync(cancellationToken);
        return rows.Select(row => (row.RecoveryCodeId, row.CodeHash)).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> TrySpendRecoveryCode(
        Guid recoveryCodeId, DateTimeOffset usedUtc, string? usedFromIp,
        CancellationToken cancellationToken = default)
    {
        var updated = await _dbContext.UserTwoFactorRecoveryCodes
            .Where(c => c.RecoveryCodeId == recoveryCodeId && c.UsedUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.UsedUtc, usedUtc)
                .SetProperty(c => c.UsedFromIp, usedFromIp), cancellationToken);
        return updated == 1;
    }

    /// <inheritdoc />
    public Task<int> CountUnusedRecoveryCodes(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.UserTwoFactorRecoveryCodes
            .CountAsync(c => c.UserId == userId && c.UsedUtc == null, cancellationToken);

    /// <inheritdoc />
    public async Task AddChallenge(
        TwoFactorChallengeState challenge, CancellationToken cancellationToken = default)
    {
        _dbContext.TwoFactorChallenges.Add(new TwoFactorChallenge
        {
            ChallengeId = challenge.ChallengeId,
            UserId = challenge.UserId,
            Account = challenge.Account,
            CreatedUtc = challenge.CreatedUtc,
            ExpiresUtc = challenge.ExpiresUtc,
            Persistent = challenge.Persistent,
            Attempts = challenge.Attempts,
            IpAddress = challenge.IpAddress,
            UserAgent = challenge.UserAgent
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<TwoFactorChallengeState?> FindChallenge(
        Guid challengeId, CancellationToken cancellationToken = default) =>
        _dbContext.TwoFactorChallenges
            .Where(c => c.ChallengeId == challengeId)
            .Select(c => new TwoFactorChallengeState
            {
                ChallengeId = c.ChallengeId,
                UserId = c.UserId,
                Account = c.Account,
                CreatedUtc = c.CreatedUtc,
                ExpiresUtc = c.ExpiresUtc,
                Persistent = c.Persistent,
                Attempts = c.Attempts,
                IpAddress = c.IpAddress,
                UserAgent = c.UserAgent
            })
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<int> CountChallengeAttempt(
        Guid challengeId, CancellationToken cancellationToken = default)
    {
        // Incremented in the database rather than read and written back: two
        // wrong codes arriving together would otherwise each read the same
        // number and store the same number, and the limit would count one.
        await _dbContext.TwoFactorChallenges
            .Where(c => c.ChallengeId == challengeId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Attempts, c => c.Attempts + 1), cancellationToken);

        return await _dbContext.TwoFactorChallenges
            .Where(c => c.ChallengeId == challengeId)
            .Select(c => c.Attempts)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveChallenge(Guid challengeId, CancellationToken cancellationToken = default) =>
        _dbContext.TwoFactorChallenges
            .Where(c => c.ChallengeId == challengeId)
            .ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public Task RemoveChallengesOf(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.TwoFactorChallenges
            .Where(c => c.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> ScheduleRemoval(
        Guid userId, DateTimeOffset dueUtc, CancellationToken cancellationToken = default)
    {
        // The predicate is the decision, and the count is how the caller hears it:
        // the factor may have been switched off between the read that let this
        // link through and the write, and everything the caller does next - ending
        // the sessions, the journal entry, the letter naming a date - belongs to a
        // waiting period that this statement did not start.
        var updated = await _dbContext.UserTwoFactors
            .Where(f => f.UserId == userId && f.ConfirmedUtc != null)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.RemovalDueUtc, dueUtc), cancellationToken);
        return updated == 1;
    }

    /// <inheritdoc />
    public async Task<bool> CancelScheduledRemoval(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var updated = await _dbContext.UserTwoFactors
            .Where(f => f.UserId == userId && f.RemovalDueUtc != null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(f => f.RemovalDueUtc, (DateTimeOffset?)null), cancellationToken);
        return updated == 1;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> FindRemovalsDue(
        DateTimeOffset now, CancellationToken cancellationToken = default) =>
        await _dbContext.UserTwoFactors
            .Where(f => f.RemovalDueUtc != null && f.RemovalDueUtc <= now)
            .Select(f => f.UserId)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int> DeleteAbandonedSetups(
        DateTimeOffset issuedBefore, CancellationToken cancellationToken = default) =>
        _dbContext.UserTwoFactors
            .Where(f => f.ConfirmedUtc == null && f.CreatedUtc < issuedBefore)
            .ExecuteDeleteAsync(cancellationToken);
}
