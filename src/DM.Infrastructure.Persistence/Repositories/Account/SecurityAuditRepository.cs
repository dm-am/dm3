using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Account.Features.Security;
using Microsoft.EntityFrameworkCore;
using static DM.Domain.Core.Parsing.UserAgentParser;
using DbEntry = DM.Infrastructure.Persistence.Entities.Account.SecurityAuditEntry;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <inheritdoc />
internal class SecurityAuditRepository : ISecurityAuditRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <summary>
    /// Creates a new security audit repository
    /// </summary>
    public SecurityAuditRepository(
        DmDbContext dbContext,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
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
            SecurityAuditEntryId = _guidFactory.Create(),
            UserId = userId,
            EventType = eventType,
            // The retention sweep expires the entry by this field, so the
            // storage lifetime is set by the injected clock, not by the host's.
            TimestampUtc = _dateTimeProvider.Now.UtcDateTime,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceInfo = Parse(userAgent),
            Details = details
        };

        _dbContext.SecurityAuditEntries.Add(entry);
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecurityAuditEntry>> GetRecentEventsAsync(Guid userId, int limit = 50)
    {
        var entries = await _dbContext.SecurityAuditEntries
            .TagWith("DM.Security.RecentEvents")
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.TimestampUtc)
            .Take(limit)
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
        var entries = await _dbContext.SecurityAuditEntries
            .TagWith("DM.Security.EventsByTypes")
            .Where(e => e.UserId == userId && eventTypes.Contains(e.EventType))
            .OrderByDescending(e => e.TimestampUtc)
            .Take(limit)
            .ToListAsync();

        return entries.Select(ToDto).ToList();
    }

    private static SecurityAuditEntry ToDto(DbEntry entry) => new()
    {
        Id = entry.SecurityAuditEntryId,
        UserId = entry.UserId,
        EventType = entry.EventType,
        TimestampUtc = new DateTimeOffset(entry.TimestampUtc, TimeSpan.Zero),
        IpAddress = entry.IpAddress,
        DeviceInfo = entry.DeviceInfo,
        Details = entry.Details
    };
}
