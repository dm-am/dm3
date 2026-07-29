using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Account.Features.Authentication;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbUserLoginRecord = DM.Infrastructure.Persistence.Entities.Account.UserLoginRecord;

namespace DM.Infrastructure.Persistence.Repositories.Account;

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
        var dbRecord = new DbUserLoginRecord
        {
            UserLoginRecordId = record.UserLoginRecordId,
            UserId = record.UserId,
            IpAddress = record.IpAddress,
            UserAgent = record.UserAgent,
            LoginUtc = record.LoginUtc,
            IsSuccessful = record.IsSuccessful
        };
        await _dbContext.UserLoginRecords.AddAsync(dbRecord);
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<Guid?> TryResolveUserId(string identifier)
    {
        // Check if identifier looks like an email
        var isEmail = identifier.Contains('@');
        var normalizedIdentifier = identifier.ToLower();

        return await _dbContext.Users
            .TagWith("DM.LoginRecord.TryResolveUserId")
            .Where(u => isEmail
                ? u.Email.ToLower() == normalizedIdentifier
                : u.Username.ToLower() == normalizedIdentifier)
            .Select(u => (Guid?)u.UserId)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserIpInfo>> GetUserIps(Guid userId, int days = 365)
    {
        var cutoff = _dateTimeProvider.Now.AddDays(-days);

        // Aggregates go into an anonymous type, not straight into UserIpInfo:
        // a constructor call inside a GroupBy projection has no translation,
        // and the whole query threw — the moderated profile answered 500.
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
    public async Task<IReadOnlyList<LinkedProfile>> GetLinkedProfiles(Guid userId, int days = 365)
    {
        var cutoff = _dateTimeProvider.Now.AddDays(-days);

        // Get all IPs used by the target user (successful logins only)
        var userIps = _dbContext.UserLoginRecords
            .Where(r => r.UserId == userId && r.IsSuccessful && r.LoginUtc >= cutoff)
            .Select(r => r.IpAddress)
            .Distinct();

        // Other users who logged in from the same IPs, collapsed to one row per
        // (user, ip) first. Counting distinct IPs inside the group projection —
        // g.Select(r => r.IpAddress).Distinct().Count() — is what EF cannot
        // translate: the whole query threw and the moderated profile answered
        // 500. Grouping twice says the same thing in SQL the provider knows.
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

        // The regroup is in memory on purpose. Counting distinct IPs inside a
        // group projection has no translation, and grouping a second time over
        // an already-grouped subquery has none either — both threw, and the
        // moderated profile answered 500. One row per (linked user, shared IP)
        // is a handful even for a busy address, so the set materialised above
        // is small by construction.
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
    public async Task<IReadOnlyList<UserLoginRecord>> GetLoginHistory(Guid userId, int limit = 50)
    {
        var dbRecords = await _dbContext.UserLoginRecords
            .TagWith("DM.LoginRecord.GetLoginHistory")
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.LoginUtc)
            .Take(limit)
            .ToListAsync();

        return dbRecords.Select(r => new UserLoginRecord
        {
            UserLoginRecordId = r.UserLoginRecordId,
            UserId = r.UserId,
            IpAddress = r.IpAddress,
            UserAgent = r.UserAgent,
            LoginUtc = r.LoginUtc,
            IsSuccessful = r.IsSuccessful
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> HasLoginFromIp(Guid userId, string ipAddress)
    {
        // Check for any successful login from this IP in the last 90 days
        var cutoff = _dateTimeProvider.Now.AddDays(-90);

        return await _dbContext.UserLoginRecords
            .TagWith("DM.LoginRecord.HasLoginFromIp")
            .AnyAsync(r => r.UserId == userId
                           && r.IpAddress == ipAddress
                           && r.IsSuccessful
                           && r.LoginUtc >= cutoff);
    }
}
