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
            .Select(u => new TwoFactorAccount(u.UserId, u.Username, u.Email, u.Role))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<TwoFactorAccount?> FindAccountByUsername(
        string username, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        return _dbContext.Users
            .Where(u => !u.IsRemoved && u.Username.ToLower() == normalized)
            .Select(u => new TwoFactorAccount(u.UserId, u.Username, u.Email, u.Role))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<TwoFactorAccount?> FindAccount(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.Users
            .Where(u => !u.IsRemoved && u.UserId == userId)
            .Select(u => new TwoFactorAccount(u.UserId, u.Username, u.Email, u.Role))
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task IssueSecret(
        Guid userId, string encryptedSecret, DateTimeOffset createdUtc,
        CancellationToken cancellationToken = default)
    {
        // Both halves or neither: a refusal between them would leave the account
        // with the previous secret gone and no new one in its place, on the one
        // screen a person reaches when they are trying to make the account safer.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // Only an unconfirmed row is replaced. A confirmed one is refused
            // above this layer, and the predicate is repeated here so that the
            // refusal does not depend on the caller remembering it.
            await _dbContext.UserTwoFactors
                .Where(f => f.UserId == userId && f.ConfirmedUtc == null)
                .ExecuteDeleteAsync(cancellationToken);

            _dbContext.UserTwoFactors.Add(new UserTwoFactor
            {
                UserId = userId,
                Secret = encryptedSecret,
                CreatedUtc = createdUtc,
                LastAcceptedStep = 0
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<bool> Confirm(
        Guid userId, DateTimeOffset confirmedUtc, IReadOnlyList<byte[]> recoveryCodeHashes,
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

            var updated = await _dbContext.UserTwoFactors
                .Where(f => f.UserId == userId && f.ConfirmedUtc == null)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(f => f.ConfirmedUtc, confirmedUtc), cancellationToken);

            switchedOn = updated == 1;
            if (!switchedOn)
            {
                // Two confirmations raced and this one lost. There is nothing to
                // issue: the set belongs to the confirmation that won, and this
                // caller is refused above.
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
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // The codes go with the factor. Left behind, they would be accepted
            // by the next factor the account switches on - a set of credentials
            // outliving the thing they belonged to.
            await _dbContext.UserTwoFactorRecoveryCodes
                .Where(c => c.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await _dbContext.UserTwoFactors
                .Where(f => f.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task ReplaceRecoveryCodes(
        Guid userId, IReadOnlyList<byte[]> codeHashes, DateTimeOffset issuedUtc,
        CancellationToken cancellationToken = default)
    {
        var rows = RecoveryCodeRows(userId, codeHashes, issuedUtc);

        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            await ReplaceRecoveryCodesWith(userId, rows, issuedUtc, cancellationToken);
            await SaveOrClearTracker(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
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
    private async Task ReplaceRecoveryCodesWith(
        Guid userId, IReadOnlyList<UserTwoFactorRecoveryCode> rows, DateTimeOffset issuedUtc,
        CancellationToken cancellationToken)
    {
        // The previous set goes whole. Half old and half new means a code
        // crossed off on paper still opens the account.
        await _dbContext.UserTwoFactorRecoveryCodes
            .Where(c => c.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        _dbContext.UserTwoFactorRecoveryCodes.AddRange(rows);
        await _dbContext.UserTwoFactors
            .Where(f => f.UserId == userId)
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
    public Task ScheduleRemoval(
        Guid userId, DateTimeOffset dueUtc, CancellationToken cancellationToken = default) =>
        _dbContext.UserTwoFactors
            .Where(f => f.UserId == userId && f.ConfirmedUtc != null)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.RemovalDueUtc, dueUtc), cancellationToken);

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
