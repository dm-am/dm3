using System;
using System.Collections.Generic;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Moderation.Bans;

/// <summary>
/// Ban type
/// </summary>
public enum BanType
{
    /// <summary>
    /// Automatic ban triggered by warning points
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Temporary ban issued by moderator
    /// </summary>
    Temporary = 1,

    /// <summary>
    /// Permanent ban issued by moderator
    /// </summary>
    Permanent = 2,

    /// <summary>
    /// Voluntary ban requested by user
    /// </summary>
    Voluntary = 3
}

/// <summary>
/// Ban DTO
/// </summary>
public class Ban
{
    /// <summary>
    /// Ban identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Banned user
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Moderator who issued the ban
    /// </summary>
    public User? Moderator { get; set; }

    /// <summary>
    /// Ban type
    /// </summary>
    public BanType Type { get; set; }

    /// <summary>
    /// Ban start timestamp (UTC)
    /// </summary>
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>
    /// Ban end timestamp (UTC), null for permanent
    /// </summary>
    public DateTimeOffset? ExpiresUtc { get; set; }

    /// <summary>
    /// Ban reason/comment
    /// </summary>
    public string Comment { get; set; } = "";

    /// <summary>
    /// Whether the ban is currently active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Whether this is a voluntary ban requested by user
    /// </summary>
    public bool IsVoluntary { get; set; }

    /// <summary>
    /// When the ban was lifted (if lifted early)
    /// </summary>
    public DateTimeOffset? LiftedUtc { get; set; }

    /// <summary>
    /// Who lifted the ban
    /// </summary>
    public User? LiftedBy { get; set; }
}

/// <summary>
/// Request to create a ban
/// </summary>
public class CreateBanRequest
{
    /// <summary>
    /// Target user login
    /// </summary>
    /// <example>problemuser</example>
    public string UserLogin { get; set; } = "";

    /// <summary>
    /// Ban type
    /// </summary>
    public BanType Type { get; set; } = BanType.Temporary;

    /// <summary>
    /// Ban expiration time (required for Temporary)
    /// </summary>
    public DateTimeOffset? ExpiresUtc { get; set; }

    /// <summary>
    /// Ban duration in hours (alternative to ExpiresUtc)
    /// </summary>
    public int? DurationHours { get; set; }

    /// <summary>
    /// Ban reason/comment
    /// </summary>
    /// <example>Repeated rule violations</example>
    public string Comment { get; set; } = "";
}

/// <summary>
/// Request to lift a ban early
/// </summary>
public class LiftBanRequest
{
    /// <summary>
    /// Reason for lifting the ban early
    /// </summary>
    /// <example>User appealed successfully</example>
    public string? Reason { get; set; }
}

/// <summary>
/// User ban status
/// </summary>
public class UserBanStatus
{
    /// <summary>
    /// User login
    /// </summary>
    public string Login { get; set; } = "";

    /// <summary>
    /// Whether user is currently banned
    /// </summary>
    public bool IsBanned { get; set; }

    /// <summary>
    /// Active ban details (if banned)
    /// </summary>
    public Ban? ActiveBan { get; set; }

    /// <summary>
    /// Ban history
    /// </summary>
    public IEnumerable<Ban> History { get; set; } = Array.Empty<Ban>();
}
