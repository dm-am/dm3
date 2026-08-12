using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Security;

/// <summary>
/// Storage of security audit events: one write and five reads, no rules
/// </summary>
/// <remarks>
/// Named for what it is. Under its former name the boundary rule, which matches
/// on the shape of a dependency, could not tell it from a domain service, and an
/// API service held it directly for that reason alone. What a caller of the
/// journal actually wants is <see cref="ISecurityJournalService" />.
/// </remarks>
public interface ISecurityAuditRepository
{
    /// <summary>
    /// Log a security event
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="eventType">Type of security event</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">Client User-Agent string</param>
    /// <param name="details">Optional additional details</param>
    Task LogAsync(
        Guid userId,
        SecurityEventType eventType,
        string? ipAddress = null,
        string? userAgent = null,
        string? details = null);

    /// <summary>
    /// Get recent security events for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="limit">Maximum number of events to return</param>
    Task<IReadOnlyList<SecurityAuditEntry>> GetRecentEventsAsync(Guid userId, int limit = 50);

    /// <summary>
    /// Get the events of one category for a user, newest first
    /// </summary>
    /// <remarks>
    /// One read for every filter the journal offers. It used to be four methods
    /// with the same query written out four times, differing only in the set of
    /// types each built for itself — and those sets are a statement about the
    /// product, so they now live in <see cref="SecurityEventCategories" /> beside
    /// the enum they select from.
    /// </remarks>
    /// <param name="userId">User identifier</param>
    /// <param name="eventTypes">Types the category is made of</param>
    /// <param name="limit">Maximum number of events to return</param>
    Task<IReadOnlyList<SecurityAuditEntry>> GetByTypesAsync(
        Guid userId, IReadOnlyList<SecurityEventType> eventTypes, int limit = 20);
}
