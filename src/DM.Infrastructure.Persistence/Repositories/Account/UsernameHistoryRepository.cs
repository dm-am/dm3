using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Core.Users;
using Microsoft.EntityFrameworkCore;
using DbUsernameHistory = DM.Infrastructure.Persistence.Entities.Account.UsernameHistory;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc />
internal class UsernameHistoryRepository : IUsernameHistoryRepository
{
    private readonly DmDbContext _dbContext;

    public UsernameHistoryRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<UsernameHistoryEntry>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.UsernameHistories
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.ChangedUtc)
            .Select(h => new UsernameHistoryEntry
            {
                UsernameHistoryId = h.UsernameHistoryId,
                OldUsername = h.OldUsername,
                NewUsername = h.NewUsername,
                ChangedUtc = h.ChangedUtc,
                ApprovedByUsername = h.ApprovedBy != null ? h.ApprovedBy.Username : null
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> IsUsernameReserved(string username, CancellationToken ct = default)
    {
        return await _dbContext.UsernameHistories
            .AnyAsync(h => h.OldUsername.ToLower() == username.ToLower(), ct);
    }

    /// <inheritdoc />
    public async Task<bool> IsUsernameReservedForOthers(string username, Guid excludeUserId, CancellationToken ct = default)
    {
        return await _dbContext.UsernameHistories
            .AnyAsync(h => h.OldUsername.ToLower() == username.ToLower() && h.UserId != excludeUserId, ct);
    }

    /// <inheritdoc />
    public async Task Add(CreateUsernameHistory entry, CancellationToken ct = default)
    {
        var entity = new DbUsernameHistory
        {
            UsernameHistoryId = entry.UsernameHistoryId,
            UserId = entry.UserId,
            OldUsername = entry.OldUsername,
            NewUsername = entry.NewUsername,
            ChangedUtc = entry.ChangedUtc,
            ApprovedByUserId = entry.ApprovedById
        };
        _dbContext.UsernameHistories.Add(entity);
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<UsernameHistoryEntry?> GetLatestByUserId(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.UsernameHistories
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.ChangedUtc)
            .Select(h => new UsernameHistoryEntry
            {
                UsernameHistoryId = h.UsernameHistoryId,
                OldUsername = h.OldUsername,
                NewUsername = h.NewUsername,
                ChangedUtc = h.ChangedUtc,
                ApprovedByUsername = h.ApprovedBy != null ? h.ApprovedBy.Username : null
            })
            .FirstOrDefaultAsync(ct);
    }
}
