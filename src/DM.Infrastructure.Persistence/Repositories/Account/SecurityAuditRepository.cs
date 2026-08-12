using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Account.Features.Security;
using DM.Infrastructure.Persistence.MongoIntegration;
using MongoDB.Driver;
using static DM.Domain.Core.Parsing.UserAgentParser;
using DbEntry = DM.Infrastructure.Persistence.Entities.Account.SecurityAuditEntry;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc />
internal class SecurityAuditRepository : MongoCollectionRepository<DbEntry>, ISecurityAuditRepository
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <summary>
    /// Creates a new security audit repository
    /// </summary>
    public SecurityAuditRepository(
        DmMongoClient mongoClient,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider) : base(mongoClient)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task LogAsync(
        Guid userId,
        SecurityEventType eventType,
        string? ipAddress = null,
        string? userAgent = null,
        string? details = null)
    {
        var entry = new DbEntry
        {
            Id = _guidFactory.Create(),
            UserId = userId,
            EventType = (int)eventType,
            // The retention TTL index expires the entry by this field, so the
            // storage lifetime is set by the injected clock, not by the host's.
            TimestampUtc = _dateTimeProvider.Now.UtcDateTime,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceInfo = Parse(userAgent),
            Details = details
        };

        await Collection.InsertOneAsync(entry);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecurityAuditEntry>> GetRecentEventsAsync(Guid userId, int limit = 50)
    {
        var entries = await Collection
            .Find(e => e.UserId == userId)
            .SortByDescending(e => e.TimestampUtc)
            .Limit(limit)
            .ToListAsync();

        return entries.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecurityAuditEntry>> GetLoginHistoryAsync(Guid userId, int limit = 20)
    {
        var loginEventTypes = new[]
        {
            (int)SecurityEventType.LoginSuccess,
            (int)SecurityEventType.LoginFailure,
            (int)SecurityEventType.SuspiciousLogin
        };

        var filter = Filter.And(
            Filter.Eq(e => e.UserId, userId),
            Filter.In(e => e.EventType, loginEventTypes));

        var entries = await Collection
            .Find(filter)
            .SortByDescending(e => e.TimestampUtc)
            .Limit(limit)
            .ToListAsync();

        return entries.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecurityAuditEntry>> GetSuccessfulLoginsAsync(Guid userId, int limit = 20)
    {
        var filter = Filter.And(
            Filter.Eq(e => e.UserId, userId),
            Filter.Eq(e => e.EventType, (int)SecurityEventType.LoginSuccess));

        var entries = await Collection
            .Find(filter)
            .SortByDescending(e => e.TimestampUtc)
            .Limit(limit)
            .ToListAsync();

        return entries.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecurityAuditEntry>> GetPasswordEventsAsync(Guid userId, int limit = 20)
    {
        var passwordEventTypes = new[]
        {
            (int)SecurityEventType.PasswordChange,
            (int)SecurityEventType.PasswordResetRequest,
            (int)SecurityEventType.PasswordResetComplete
        };

        var filter = Filter.And(
            Filter.Eq(e => e.UserId, userId),
            Filter.In(e => e.EventType, passwordEventTypes));

        var entries = await Collection
            .Find(filter)
            .SortByDescending(e => e.TimestampUtc)
            .Limit(limit)
            .ToListAsync();

        return entries.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecurityAuditEntry>> GetSessionEventsAsync(Guid userId, int limit = 20)
    {
        var sessionEventTypes = new[]
        {
            (int)SecurityEventType.Logout,
            (int)SecurityEventType.SessionTerminated,
            (int)SecurityEventType.LogoutElsewhere
        };

        var filter = Filter.And(
            Filter.Eq(e => e.UserId, userId),
            Filter.In(e => e.EventType, sessionEventTypes));

        var entries = await Collection
            .Find(filter)
            .SortByDescending(e => e.TimestampUtc)
            .Limit(limit)
            .ToListAsync();

        return entries.Select(ToDto).ToList();
    }

    private static SecurityAuditEntry ToDto(DbEntry entry) => new()
    {
        Id = entry.Id,
        UserId = entry.UserId,
        EventType = (SecurityEventType)entry.EventType,
        TimestampUtc = new DateTimeOffset(entry.TimestampUtc, TimeSpan.Zero),
        IpAddress = entry.IpAddress,
        DeviceInfo = entry.DeviceInfo,
        Details = entry.Details
    };
}
