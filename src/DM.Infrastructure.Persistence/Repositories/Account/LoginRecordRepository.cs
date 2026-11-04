using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Account.Features.Authentication;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc cref="ILoginRecordRepository" />
internal class LoginRecordRepository : ILoginRecordRepository
{
    /// <summary>How far back the IP views of moderation look.</summary>
    private const int IpLookbackDays = 365;

    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

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
        await _dbContext.UserLoginRecords.AddAsync(record.ToRow());
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<Guid?> TryResolveUserId(string identifier)
    {
        return await _dbContext.Users
            .TagWith("DM.LoginRecord.TryResolveUserId")
            .Where(identifier.Contains('@')
                ? AccountNames.EmailIs(identifier)
                : AccountNames.UsernameIs(identifier))
            .Select(u => (Guid?)u.UserId)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserIpInfo>> GetUserIps(Guid userId)
    {
        var cutoff = _dateTimeProvider.Now.AddDays(-IpLookbackDays);

        // An anonymous type: a constructor call inside a GroupBy projection has no translation.
        var grouped = await _dbContext.UserLoginRecords
            .TagWith("DM.LoginRecord.GetUserIps")
            .Where(r => r.UserId == userId && r.IsSuccessful && r.LoginUtc >= cutoff)
            .GroupBy(r => r.IpAddress)
            .Select(g => new
            {
                IpAddress = g.Key,
                FirstSeenUtc = g.Min(r => r.LoginUtc),
                LastSeenUtc = g.Max(r => r.LoginUtc),
                LoginsCount = g.Count()
            })
            .OrderByDescending(x => x.LastSeenUtc)
            .ToListAsync();

        return grouped
            .Select(x => new UserIpInfo(x.IpAddress, x.FirstSeenUtc, x.LastSeenUtc, x.LoginsCount))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LinkedProfile>> GetLinkedProfiles(Guid userId)
    {
        var cutoff = _dateTimeProvider.Now.AddDays(-IpLookbackDays);

        // Get all IPs used by the target user (successful logins only)
        var userIps = _dbContext.UserLoginRecords
            .Where(r => r.UserId == userId && r.IsSuccessful && r.LoginUtc >= cutoff)
            .Select(r => r.IpAddress)
            .Distinct();

        // Other users who logged in from the same IPs, one row per (user, ip) first:
        // EF cannot translate a distinct count inside the group.
        var perUserAndIp = await _dbContext.UserLoginRecords
            .TagWith("DM.LoginRecord.GetLinkedProfiles")
            .Where(r => r.UserId != userId && r.IsSuccessful && r.LoginUtc >= cutoff)
            .Where(r => userIps.Contains(r.IpAddress))
            .GroupBy(r => new { r.UserId, r.User.Username, r.IpAddress })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.Username,
                LastLoginUtc = g.Max(r => r.LoginUtc)
            })
            .ToListAsync();

        // Regrouped in memory: neither a distinct count inside a group projection nor a
        // second GroupBy has a translation, and the set above is small by construction.
        return perUserAndIp
            .GroupBy(x => new { x.UserId, x.Username })
            .Select(g => new LinkedProfile(
                g.Key.UserId,
                g.Key.Username,
                g.Count(),
                g.Max(x => x.LastLoginUtc)))
            .OrderByDescending(lp => lp.SharedIpsCount)
            .ThenByDescending(lp => lp.LastSharedLoginUtc)
            .ToList();
    }

    /// <inheritdoc />
    public Task<int> CountLoginHistory(Guid userId) => _dbContext.UserLoginRecords
        .TagWith("DM.LoginRecord.CountLoginHistory")
        .CountAsync(r => r.UserId == userId);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserLoginRecord>> GetLoginHistory(Guid userId, int skip, int take)
    {
        // The page is the caller's, not a default hidden in a parameter. It used
        // to be Take(50) with no way to ask for anything else and no total beside
        // it, so an administrator reading an incident saw fifty logins and could
        // not tell from the answer that there were more.
        var dbRecords = await _dbContext.UserLoginRecords
            .TagWith("DM.LoginRecord.GetLoginHistory")
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.LoginUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return dbRecords.Select(r => r.ToLoginRecord()).ToList();
    }
}
