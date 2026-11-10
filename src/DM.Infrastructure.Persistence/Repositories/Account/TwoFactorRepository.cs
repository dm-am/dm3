using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.TwoFactor;
using DM.Domain.Core.Abstractions;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Users;
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
            .ProjectToTwoFactorState()
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<TwoFactorAccount?> FindAccountByEmail(
        string email, CancellationToken cancellationToken = default)
    {
        return FindAccountWhere(AccountNames.EmailIs(email.Trim()), cancellationToken);
    }

    /// <inheritdoc />
    public Task<TwoFactorAccount?> FindAccountByUsername(
        string username, CancellationToken cancellationToken = default)
    {
        return FindAccountWhere(AccountNames.UsernameIs(username.Trim()), cancellationToken);
    }

    /// <inheritdoc />
    public Task<TwoFactorAccount?> FindAccount(Guid userId, CancellationToken cancellationToken = default) =>
        FindAccountWhere(u => u.UserId == userId, cancellationToken);

    private Task<TwoFactorAccount?> FindAccountWhere(
        Expression<Func<User, bool>> predicate, CancellationToken cancellationToken) =>
        _dbContext.Users
            .Where(u => !u.IsRemoved)
            .Where(predicate)
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
        // One statement, so two setups started at once cannot both insert. Only an
        // unconfirmed row is replaced (zero rows is the caller's refusal), and the
        // replacement resets every column, so a new setup begins on nothing.
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

        // Both halves or neither: the set is shown once, so a factor without its set
        // ties the owner to one device.
        var switchedOn = false;
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // The issue moment as well as the stamp: a setup started elsewhere replaces
            // the row in place, and one predicate cannot go stale between read and write.
            var updated = await _dbContext.UserTwoFactors
                .Where(f => f.UserId == userId &&
                            f.CreatedUtc == createdUtc &&
                            f.ConfirmedUtc == null)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(f => f.ConfirmedUtc, confirmedUtc), cancellationToken);

            switchedOn = updated == 1;
            if (!switchedOn)
            {
                // The secret is gone: replaced, or confirmed by a call that came first.
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
                // The factor was taken off since the caller read it; both the staged codes
                // and the deletion go back, or the next factor would accept this set.
                _dbContext.ChangeTracker.Clear();
                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            await SaveOrClearTracker(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        return replaced;
    }

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
        // A confirmed factor, not merely a row: an unconfirmed setup is off. The confirmation
        // stamps the moment in this transaction, so it passes here.
        return await _dbContext.UserTwoFactors
            .Where(f => f.UserId == userId && f.ConfirmedUtc != null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(f => f.RecoveryCodesIssuedUtc, issuedUtc), cancellationToken);
    }

    /// <summary>
    /// Save, and leave nothing of a failed save behind in the tracker.
    /// </summary>
    /// <remarks>
    /// The context is scoped to the request: rows left staged would be saved by whatever saves next.
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
        _dbContext.TwoFactorChallenges.Add(challenge.ToRow());
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<TwoFactorChallengeState?> FindChallenge(
        Guid challengeId, CancellationToken cancellationToken = default) =>
        _dbContext.TwoFactorChallenges
            .Where(c => c.ChallengeId == challengeId)
            .ProjectToChallengeState()
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
        // The predicate is the decision: the factor may have been switched off since the
        // caller read it, and the count says whether this statement started the wait.
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
