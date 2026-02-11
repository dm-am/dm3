using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Authentication.Repositories;

/// <inheritdoc cref="ILoginRecordRepository" />
internal class LoginRecordRepository : ILoginRecordRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public LoginRecordRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task Record(UserLoginRecord record)
    {
        await _dbContext.UserLoginRecords.AddAsync(record);
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<Guid?> TryResolveUserId(string identifier)
    {
        // Check if identifier looks like an email
        var isEmail = identifier.Contains('@');
        var normalizedIdentifier = identifier.ToLower();

        return await _dbContext.Users
            .Where(u => isEmail
                ? u.Email.ToLower() == normalizedIdentifier
                : u.Login.ToLower() == normalizedIdentifier)
            .Select(u => (Guid?)u.UserId)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserIpInfo>> GetUserIps(Guid userId, int days = 365)
    {
        var cutoff = _dateTimeProvider.Now.AddDays(-days);

        return await _dbContext.UserLoginRecords
            .Where(r => r.UserId == userId && r.IsSuccessful && r.LoginUtc >= cutoff)
            .GroupBy(r => r.IpAddress)
            .Select(g => new UserIpInfo(
                g.Key,
                g.Min(r => r.LoginUtc),
                g.Max(r => r.LoginUtc),
                g.Count()))
            .OrderByDescending(ip => ip.LastSeenUtc)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LinkedProfile>> GetLinkedProfiles(Guid userId, int days = 365)
    {
        var cutoff = _dateTimeProvider.Now.AddDays(-days);

        // Get all IPs used by the target user (successful logins only)
        var userIps = _dbContext.UserLoginRecords
            .Where(r => r.UserId == userId && r.IsSuccessful && r.LoginUtc >= cutoff)
            .Select(r => r.IpAddress)
            .Distinct();

        // Find other users who logged in from the same IPs
        return await _dbContext.UserLoginRecords
            .Where(r => r.UserId != userId && r.IsSuccessful && r.LoginUtc >= cutoff)
            .Where(r => userIps.Contains(r.IpAddress))
            .GroupBy(r => new { r.UserId, r.User.Login })
            .Select(g => new LinkedProfile(
                g.Key.UserId,
                g.Key.Login,
                g.Select(r => r.IpAddress).Distinct().Count(),
                g.Max(r => r.LoginUtc)))
            .OrderByDescending(lp => lp.SharedIpCount)
            .ThenByDescending(lp => lp.LastSharedLoginUtc)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserLoginRecord>> GetLoginHistory(Guid userId, int limit = 50)
    {
        return await _dbContext.UserLoginRecords
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.LoginUtc)
            .Take(limit)
            .ToListAsync();
    }
}
