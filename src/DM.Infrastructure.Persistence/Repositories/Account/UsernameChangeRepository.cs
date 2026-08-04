using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Shared.Users;
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
    public async Task<UsernameChangeRequest?> GetPendingByUserId(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.UsernameChangeRequests
            .Where(r => r.UserId == userId && r.Status == UsernameChangeRequestStatus.Pending)
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
        await _dbContext.SaveChangesAsync(ct);
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
    public async Task UpdateUserUsername(Guid userId, string newUsername, CancellationToken ct = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user != null)
        {
            user.Username = newUsername;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsUsernameAvailable(string username, Guid? excludeUserId = null, CancellationToken ct = default) =>
        !await AccountReservation.UsernameTaken(_dbContext.Users, username, excludeUserId, ct);

    /// <inheritdoc />
    public Task SaveChanges(CancellationToken ct = default) =>
        _dbContext.SaveChangesAsync(ct);

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
        DateTimeOffset now, string commentSuffix, CancellationToken ct = default) =>
        _dbContext.UsernameChangeRequests
            .TagWith("DM.Account.ExpireUsernameChangeApprovals")
            .Where(r => r.Status == UsernameChangeRequestStatus.Approved &&
                        r.ApprovalTokenExpiresUtc.HasValue &&
                        r.ApprovalTokenExpiresUtc.Value < now)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.Status, UsernameChangeRequestStatus.Expired)
                      .SetProperty(r => r.ApprovalToken, (Guid?)null)
                      .SetProperty(r => r.ResolverComment, r => r.ResolverComment + commentSuffix),
                ct);
}
