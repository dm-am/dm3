using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Infrastructure.Persistence.Shared.Users;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using DbUsernameChangeRequest = DM.Infrastructure.Persistence.Entities.Account.UsernameChangeRequest;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc />
internal class UsernameChangeRepository : IUsernameChangeRepository
{
    private readonly DmDbContext _dbContext;

    public UsernameChangeRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<UsernameChangeRequest?> GetActiveByUserId(
        Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        return await _dbContext.UsernameChangeRequests
            // Approved holds that status until the hourly pass moves it, so the
            // status alone would keep refusing a new request for up to an hour
            // after the approval link had already died - while the link itself
            // answered "expired" and offered nothing. The stored moment is what
            // every other reader of this row trusts, and it is trusted here too.
            .Where(r => r.UserId == userId &&
                        (r.Status == UsernameChangeRequestStatus.Pending ||
                         (r.Status == UsernameChangeRequestStatus.Approved &&
                          r.ApprovalTokenExpiresUtc.HasValue &&
                          r.ApprovalTokenExpiresUtc.Value > now)))
            .Select(r => new UsernameChangeRequest
            {
                RequestId = r.RequestId,
                UserId = r.UserId,
                RequestedUsername = r.RequestedUsername,
                Reason = r.Reason,
                Status = r.Status,
                CreatedUtc = r.CreatedUtc,
                ApprovalToken = r.ApprovalToken,
                ApprovalTokenExpiresUtc = r.ApprovalTokenExpiresUtc,
                ResolvedUtc = r.ResolvedUtc,
                ResolvedByUserId = r.ResolvedByUserId,
                ResolverComment = r.ResolverComment,
                UserEmail = r.User.Email,
                UserUsername = r.User.Username,
                ResolverUsername = r.ResolvedBy != null ? r.ResolvedBy.Username : null
            })
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<UsernameChangeRequest?> GetLatestByUserId(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.UsernameChangeRequests
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedUtc)
            .Select(r => new UsernameChangeRequest
            {
                RequestId = r.RequestId,
                UserId = r.UserId,
                RequestedUsername = r.RequestedUsername,
                Reason = r.Reason,
                Status = r.Status,
                CreatedUtc = r.CreatedUtc,
                ApprovalToken = r.ApprovalToken,
                ApprovalTokenExpiresUtc = r.ApprovalTokenExpiresUtc,
                ResolvedUtc = r.ResolvedUtc,
                ResolvedByUserId = r.ResolvedByUserId,
                ResolverComment = r.ResolverComment,
                UserEmail = r.User.Email,
                UserUsername = r.User.Username,
                ResolverUsername = r.ResolvedBy != null ? r.ResolvedBy.Username : null
            })
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<UsernameChangeRequest?> GetById(Guid requestId, CancellationToken ct = default)
    {
        return await _dbContext.UsernameChangeRequests
            .Where(r => r.RequestId == requestId)
            .Select(r => new UsernameChangeRequest
            {
                RequestId = r.RequestId,
                UserId = r.UserId,
                RequestedUsername = r.RequestedUsername,
                Reason = r.Reason,
                Status = r.Status,
                CreatedUtc = r.CreatedUtc,
                ApprovalToken = r.ApprovalToken,
                ApprovalTokenExpiresUtc = r.ApprovalTokenExpiresUtc,
                ResolvedUtc = r.ResolvedUtc,
                ResolvedByUserId = r.ResolvedByUserId,
                ResolverComment = r.ResolverComment,
                UserEmail = r.User.Email,
                UserUsername = r.User.Username,
                ResolverUsername = r.ResolvedBy != null ? r.ResolvedBy.Username : null
            })
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<UsernameChangeRequestEntry>> GetPendingRequests(CancellationToken ct = default)
    {
        return await _dbContext.UsernameChangeRequests
            .Where(r => r.Status == UsernameChangeRequestStatus.Pending)
            .OrderBy(r => r.CreatedUtc)
            .Select(r => new UsernameChangeRequestEntry
            {
                RequestId = r.RequestId,
                CurrentUsername = r.User.Username,
                UserId = r.UserId,
                RequestedUsername = r.RequestedUsername,
                Reason = r.Reason,
                Status = r.Status,
                CreatedUtc = r.CreatedUtc
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<UsernameChangeRequest?> GetByApprovalToken(Guid token, CancellationToken ct = default)
    {
        return await _dbContext.UsernameChangeRequests
            .Where(r => r.ApprovalToken == token)
            .Select(r => new UsernameChangeRequest
            {
                RequestId = r.RequestId,
                UserId = r.UserId,
                RequestedUsername = r.RequestedUsername,
                Reason = r.Reason,
                Status = r.Status,
                CreatedUtc = r.CreatedUtc,
                ApprovalToken = r.ApprovalToken,
                ApprovalTokenExpiresUtc = r.ApprovalTokenExpiresUtc,
                ResolvedUtc = r.ResolvedUtc,
                ResolvedByUserId = r.ResolvedByUserId,
                ResolverComment = r.ResolverComment,
                UserEmail = r.User.Email,
                UserUsername = r.User.Username,
                ResolverUsername = r.ResolvedBy != null ? r.ResolvedBy.Username : null
            })
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task Add(UsernameChangeRequest request, CancellationToken ct = default)
    {
        var entity = new DbUsernameChangeRequest
        {
            RequestId = request.RequestId,
            UserId = request.UserId,
            RequestedUsername = request.RequestedUsername,
            Reason = request.Reason,
            Status = request.Status,
            CreatedUtc = request.CreatedUtc,
            ApprovalToken = request.ApprovalToken,
            ApprovalTokenExpiresUtc = request.ApprovalTokenExpiresUtc,
            ResolvedUtc = request.ResolvedUtc,
            ResolvedByUserId = request.ResolvedByUserId,
            ResolverComment = request.ResolverComment
        };
        _dbContext.UsernameChangeRequests.Add(entity);
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // The service checks for a request in flight before it writes; this is
            // the race where two requests sent at once both pass that check. The
            // partial unique index is what actually holds the rule, and the error
            // code is translated here so the domain need not know the engine's.
            _dbContext.ChangeTracker.Clear();
            throw new DuplicateEntityException("Duplicate username change request", ex);
        }
    }

    /// <inheritdoc />
    public async Task Update(UsernameChangeRequest request, CancellationToken ct = default)
    {
        var entity = await _dbContext.UsernameChangeRequests
            .FirstOrDefaultAsync(r => r.RequestId == request.RequestId, ct);

        if (entity == null) return;

        entity.RequestedUsername = request.RequestedUsername;
        entity.Reason = request.Reason;
        entity.Status = request.Status;
        entity.ApprovalToken = request.ApprovalToken;
        entity.ApprovalTokenExpiresUtc = request.ApprovalTokenExpiresUtc;
        entity.ResolvedUtc = request.ResolvedUtc;
        entity.ResolvedByUserId = request.ResolvedByUserId;
        entity.ResolverComment = request.ResolverComment;

        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ApplyRename(
        UsernameChangeRequest request, CreateUsernameHistory history, CancellationToken ct = default)
    {
        var entity = await _dbContext.UsernameChangeRequests
            .FirstOrDefaultAsync(r => r.RequestId == request.RequestId, ct);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == request.UserId, ct);

        if (entity == null || user == null) return;

        _dbContext.UsernameHistories.Add(new Entities.Account.UsernameHistory
        {
            UsernameHistoryId = history.UsernameHistoryId,
            UserId = history.UserId,
            OldUsername = history.OldUsername,
            NewUsername = history.NewUsername,
            ChangedUtc = history.ChangedUtc,
            ApprovedByUserId = history.ApprovedById
        });

        user.Username = history.NewUsername;

        entity.RequestedUsername = request.RequestedUsername;
        entity.Status = request.Status;
        entity.ApprovalToken = request.ApprovalToken;
        entity.ApprovalTokenExpiresUtc = request.ApprovalTokenExpiresUtc;
        entity.ResolvedUtc = request.ResolvedUtc;
        entity.ResolvedByUserId = request.ResolvedByUserId;
        entity.ResolverComment = request.ResolverComment;

        // All three rows are tracked, so one SaveChanges is the transaction and an
        // explicit one here would be machinery around a guarantee already given.
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> IsUsernameAvailable(string username, Guid? excludeUserId = null, CancellationToken ct = default) =>
        !await AccountReservation.UsernameTaken(_dbContext.Users, username, excludeUserId, ct);


    /// <inheritdoc />
    public Task<int> ExpireUnreviewedRequests(
        DateTimeOffset createdBefore,
        DateTimeOffset resolvedUtc,
        string comment,
        CancellationToken ct = default) =>
        _dbContext.UsernameChangeRequests
            .TagWith("DM.Account.ExpireUnreviewedUsernameChanges")
            .Where(r => r.Status == UsernameChangeRequestStatus.Pending && r.CreatedUtc < createdBefore)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.Status, UsernameChangeRequestStatus.Expired)
                      .SetProperty(r => r.ResolvedUtc, resolvedUtc)
                      .SetProperty(r => r.ResolverComment, comment),
                ct);

    /// <inheritdoc />
    public Task<int> ExpireApprovalTokens(
        DateTimeOffset now, string expiredReason, CancellationToken ct = default) =>
        _dbContext.UsernameChangeRequests
            .TagWith("DM.Account.ExpireUsernameChangeApprovals")
            .Where(r => r.Status == UsernameChangeRequestStatus.Approved &&
                        r.ApprovalTokenExpiresUtc.HasValue &&
                        r.ApprovalTokenExpiresUtc.Value < now)
            // Expired is what withdraws the approval; the service refuses anything
            // that is not Approved. The token stays on the row so the page opened
            // from the letter can still find it and say that it ran out, instead of
            // finding nothing and calling the link invalid.
            // Same rule as ResolutionComment.Join, restated as an expression the
            // provider can translate: no comment means the reason stands alone
            // rather than after a bar with nothing before it, and the result is
            // cut to the column length. The cut matters more here than anywhere
            // else - this is one statement over every lapsed approval, so a
            // single overlong comment would roll back the whole pass, hourly and
            // without a sound.
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.Status, UsernameChangeRequestStatus.Expired)
                      .SetProperty(r => r.ResolverComment, r =>
                          (r.ResolverComment == null || r.ResolverComment == string.Empty
                              ? expiredReason
                              : r.ResolverComment + ResolutionComment.Separator + expiredReason)
                          .Substring(0, ResolutionComment.MaxLength)),
                ct);
}
