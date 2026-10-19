using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;
using DbUsernameChangeRequest = DM.Infrastructure.Persistence.Entities.Account.UsernameChangeRequest;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc />
internal class UsernameChangeRepository : IUsernameChangeRepository
{
    private static readonly Expression<Func<DbUsernameChangeRequest, UsernameChangeRequest>> Projection =
        r => new UsernameChangeRequest
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
        };

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
            // The stored expiry, not the status alone: Approved holds until the hourly pass moves it.
            .Where(r => r.UserId == userId &&
                        (r.Status == UsernameChangeRequestStatus.Pending ||
                         (r.Status == UsernameChangeRequestStatus.Approved &&
                          r.ApprovalTokenExpiresUtc > now)))
            .Select(Projection)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<UsernameChangeRequest?> GetLatestByUserId(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.UsernameChangeRequests
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedUtc)
            .Select(Projection)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<UsernameChangeRequest?> GetById(Guid requestId, CancellationToken ct = default)
    {
        return await _dbContext.UsernameChangeRequests
            .Where(r => r.RequestId == requestId)
            .Select(Projection)
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
            .Select(Projection)
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
        catch (DbUpdateException ex) when (ex.IsDuplicateKey())
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

        entity.Reason = request.Reason;
        ApplyDecision(entity, request);

        await _dbContext.SaveChangesAsync(ct);
    }

    /// <summary>The fields a decision on the request writes.</summary>
    private static void ApplyDecision(DbUsernameChangeRequest entity, UsernameChangeRequest request)
    {
        entity.RequestedUsername = request.RequestedUsername;
        entity.Status = request.Status;
        entity.ApprovalToken = request.ApprovalToken;
        entity.ApprovalTokenExpiresUtc = request.ApprovalTokenExpiresUtc;
        entity.ResolvedUtc = request.ResolvedUtc;
        entity.ResolvedByUserId = request.ResolvedByUserId;
        entity.ResolverComment = request.ResolverComment;
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
        ApplyDecision(entity, request);

        // All three rows are tracked, so one SaveChanges is the transaction.
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsDuplicateKey())
        {
            // Refused by lower("Username") for a name taken after the check, or by the
            // history's OldUsername for a name the account leaves behind a second time.
            _dbContext.ChangeTracker.Clear();
            throw new DuplicateEntityException("Duplicate username or username history entry", ex);
        }
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
                        r.ApprovalTokenExpiresUtc < now)
            // The token stays on the row so the page from the letter can say it ran out.
            // The comment is ResolutionComment.Join as a translatable expression, cut to
            // the column length so one overlong comment cannot roll back the whole pass.
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.Status, UsernameChangeRequestStatus.Expired)
                      .SetProperty(r => r.ResolverComment, r =>
                          (r.ResolverComment == null || r.ResolverComment == string.Empty
                              ? expiredReason
                              : r.ResolverComment + ResolutionComment.Separator + expiredReason)
                          .Substring(0, ResolutionComment.MaxLength)),
                ct);
}
