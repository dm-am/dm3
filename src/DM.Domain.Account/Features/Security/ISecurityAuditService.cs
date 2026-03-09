using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Security;

/// <summary>
/// Service for logging and retrieving security audit events
/// </summary>
public interface ISecurityAuditService
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
    /// Get login history for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="limit">Maximum number of events to return</param>
    Task<IReadOnlyList<SecurityAuditEntry>> GetLoginHistoryAsync(Guid userId, int limit = 20);

    /// <summary>
    /// Get password-related events for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="limit">Maximum number of events to return</param>
    Task<IReadOnlyList<SecurityAuditEntry>> GetPasswordEventsAsync(Guid userId, int limit = 20);

    /// <summary>
    /// Get session-related events for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="limit">Maximum number of events to return</param>
    Task<IReadOnlyList<SecurityAuditEntry>> GetSessionEventsAsync(Guid userId, int limit = 20);
}
