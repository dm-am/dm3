using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Users.LoginHistory;

/// <inheritdoc />
internal class LoginHistoryRepository : ILoginHistoryRepository
{
    private readonly DmDbContext _dbContext;

    public LoginHistoryRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<LoginHistoryEntry>> GetByUserId(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.LoginHistories
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.ChangedUtc)
            .Select(h => new LoginHistoryEntry
            {
                LoginHistoryId = h.LoginHistoryId,
                OldLogin = h.OldLogin,
                NewLogin = h.NewLogin,
                ChangedUtc = h.ChangedUtc,
                ApprovedByLogin = h.ApprovedBy != null ? h.ApprovedBy.Login : null
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> IsLoginReserved(string login, CancellationToken ct = default)
    {
        return await _dbContext.LoginHistories
            .AnyAsync(h => EF.Functions.ILike(h.OldLogin, login), ct);
    }

    /// <inheritdoc />
    public async Task Add(DataAccess.BusinessObjects.Users.LoginHistory entry, CancellationToken ct = default)
    {
        _dbContext.LoginHistories.Add(entry);
        await _dbContext.SaveChangesAsync(ct);
    }
}
