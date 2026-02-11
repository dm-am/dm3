using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Users.LoginChange;

/// <inheritdoc />
internal class LoginChangeRepository : ILoginChangeRepository
{
    private readonly DmDbContext _dbContext;

    public LoginChangeRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<LoginChangeRequest?> GetPendingByUserId(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.LoginChangeRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Status == LoginChangeRequestStatus.Pending, ct);
    }

    /// <inheritdoc />
    public async Task<LoginChangeRequest?> GetLatestByUserId(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.LoginChangeRequests
            .Include(r => r.User)
            .Include(r => r.ResolvedBy)
            .OrderByDescending(r => r.CreatedUtc)
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);
    }

    /// <inheritdoc />
    public async Task<LoginChangeRequest?> GetById(Guid requestId, CancellationToken ct = default)
    {
        return await _dbContext.LoginChangeRequests
            .Include(r => r.User)
            .Include(r => r.ResolvedBy)
            .FirstOrDefaultAsync(r => r.RequestId == requestId, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<LoginChangeRequestEntry>> GetPendingRequests(CancellationToken ct = default)
    {
        return await _dbContext.LoginChangeRequests
            .Include(r => r.User)
            .Where(r => r.Status == LoginChangeRequestStatus.Pending)
            .OrderBy(r => r.CreatedUtc)
            .Select(r => new LoginChangeRequestEntry
            {
                RequestId = r.RequestId,
                CurrentLogin = r.User.Login,
                UserId = r.UserId,
                RequestedLogin = r.RequestedLogin,
                Reason = r.Reason,
                Status = r.Status,
                CreatedUtc = r.CreatedUtc
            })
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task Add(LoginChangeRequest request, CancellationToken ct = default)
    {
        _dbContext.LoginChangeRequests.Add(request);
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public Task SaveChanges(CancellationToken ct = default) =>
        _dbContext.SaveChangesAsync(ct);
}
