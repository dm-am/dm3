using System;

namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// DTO for creating a new authentication session
/// </summary>
public class CreateSession
{
    /// <summary>
    /// Session identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Expiration moment (UTC)
    /// </summary>
    public DateTime ExpirationDate { get; set; }

    /// <summary>
    /// Persistence flag (remember me)
    /// </summary>
    public bool Persistent { get; set; }

    /// <summary>
    /// Flag of invisible log in
    /// </summary>
    public bool Invisible { get; set; }

    /// <summary>
    /// Session creation time (UTC)
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Client IP address at session creation
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User-Agent header value
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Parsed device/browser info (derived from UserAgent)
    /// </summary>
    public string? DeviceInfo { get; set; }
}

/// <summary>
/// DTO for user login record
/// </summary>
public class UserLoginRecord
{
    /// <summary>
    /// Record identifier
    /// </summary>
    public Guid UserLoginRecordId { get; set; }

    /// <summary>
    /// User who attempted to log in
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Client IP address
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Browser User-Agent string
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// UTC timestamp of the login event
    /// </summary>
    public DateTimeOffset LoginUtc { get; set; }

    /// <summary>
    /// Whether this was a successful login
    /// </summary>
    public bool IsSuccessful { get; set; }
}

/// <summary>
/// Aggregated IP info: IP address with first/last seen timestamps and login count
/// </summary>
public record UserIpInfo(
    string IpAddress,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    int LoginCount);

/// <summary>
/// User sharing IP addresses with the target user
/// </summary>
public record LinkedProfile(
    Guid UserId,
    string Username,
    int SharedIpCount,
    DateTimeOffset LastSharedLoginUtc);
