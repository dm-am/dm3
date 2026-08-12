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
    public async Task<IReadOnlyList<SecurityAuditEntry>> GetByTypesAsync(
        Guid userId, IReadOnlyList<SecurityEventType> eventTypes, int limit = 20)
    {
        // The set arrives from the domain rather than being built here: which
        // types make up a category is a statement about the product, and four
        // copies of this query differing only in that array is what it used to be.
        var filter = Filter.And(
            Filter.Eq(e => e.UserId, userId),
            Filter.In(e => e.EventType, eventTypes.Select(type => (int)type)));

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
