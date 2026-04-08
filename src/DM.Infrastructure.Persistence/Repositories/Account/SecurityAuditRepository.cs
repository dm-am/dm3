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
internal class SecurityAuditRepository : ISecurityAuditService
{
    private readonly DmMongoClient _mongoClient;
    private readonly IGuidFactory _guidFactory;

    /// <summary>
    /// Creates a new security audit service
    /// </summary>
    public SecurityAuditRepository(
        DmMongoClient mongoClient,
        IGuidFactory guidFactory)
    {
        _mongoClient = mongoClient;
        _guidFactory = guidFactory;
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
            TimestampUtc = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceInfo = Parse(userAgent),
            Details = details
        };

        var collection = _mongoClient.GetCollection<DbEntry>();
        await collection.InsertOneAsync(entry);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecurityAuditEntry>> GetRecentEventsAsync(Guid userId, int limit = 50)
    {
        var collection = _mongoClient.GetCollection<DbEntry>();

        var entries = await collection
            .Find(e => e.UserId == userId)
            .SortByDescending(e => e.TimestampUtc)
            .Limit(limit)
            .ToListAsync();

        return entries.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecurityAuditEntry>> GetLoginHistoryAsync(Guid userId, int limit = 20)
    {
        var collection = _mongoClient.GetCollection<DbEntry>();

        var loginEventTypes = new[]
        {
            (int)SecurityEventType.LoginSuccess,
            (int)SecurityEventType.LoginFailure,
            (int)SecurityEventType.SuspiciousLogin
        };

        var filter = Builders<DbEntry>.Filter.And(
            Builders<DbEntry>.Filter.Eq(e => e.UserId, userId),
            Builders<DbEntry>.Filter.In(e => e.EventType, loginEventTypes));

        var entries = await collection
            .Find(filter)
            .SortByDescending(e => e.TimestampUtc)
            .Limit(limit)
            .ToListAsync();

        return entries.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecurityAuditEntry>> GetPasswordEventsAsync(Guid userId, int limit = 20)
    {
        var collection = _mongoClient.GetCollection<DbEntry>();

        var passwordEventTypes = new[]
        {
            (int)SecurityEventType.PasswordChange,
            (int)SecurityEventType.PasswordResetRequest,
            (int)SecurityEventType.PasswordResetComplete
        };

        var filter = Builders<DbEntry>.Filter.And(
            Builders<DbEntry>.Filter.Eq(e => e.UserId, userId),
            Builders<DbEntry>.Filter.In(e => e.EventType, passwordEventTypes));

        var entries = await collection
            .Find(filter)
            .SortByDescending(e => e.TimestampUtc)
            .Limit(limit)
            .ToListAsync();

        return entries.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecurityAuditEntry>> GetSessionEventsAsync(Guid userId, int limit = 20)
    {
        var collection = _mongoClient.GetCollection<DbEntry>();

        var sessionEventTypes = new[]
        {
            (int)SecurityEventType.Logout,
            (int)SecurityEventType.SessionTerminated,
            (int)SecurityEventType.LogoutElsewhere
        };

        var filter = Builders<DbEntry>.Filter.And(
            Builders<DbEntry>.Filter.Eq(e => e.UserId, userId),
            Builders<DbEntry>.Filter.In(e => e.EventType, sessionEventTypes));

        var entries = await collection
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
