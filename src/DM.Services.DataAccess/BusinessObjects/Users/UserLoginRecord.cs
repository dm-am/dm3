using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Services.DataAccess.BusinessObjects.Users;

/// <summary>
/// DAL model for user login record. Tracks IP addresses and user agents for moderation purposes.
/// Records are immutable (append-only) and cleaned up after 365 days.
/// </summary>
[Table("UserLoginRecords")]
public class UserLoginRecord
{
    /// <summary>
    /// Record identifier
    /// </summary>
    [Key]
    public Guid UserLoginRecordId { get; set; }

    /// <summary>
    /// User who attempted to log in
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Client IP address. Supports IPv4 and IPv6.
    /// Extracted from X-Forwarded-For (behind reverse proxy) or RemoteIpAddress.
    /// </summary>
    [MaxLength(45)]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Browser User-Agent string (truncated to 500 chars).
    /// </summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// UTC timestamp of the login event
    /// </summary>
    public DateTimeOffset LoginUtc { get; set; }

    /// <summary>
    /// Whether this was a successful login (true) or failed attempt (false)
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// User who attempted to log in
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
}
