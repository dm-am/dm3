using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Moderation.Bans;

/// <summary>
/// Ban type
/// </summary>
/// <remarks>
/// Derived from the stored ban, not sent with it: permanence is the length of the
/// window and voluntariness is a column. An Auto member used to head this list for
/// a ban issued automatically on warning points - points are clamped to 0..6 and
/// compared with no threshold anywhere, so nothing could ever produce it.
/// </remarks>
public enum BanType
{
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
    /// Whether the ban was lifted early (distinguishes lifted from expired in history)
    /// </summary>
    public bool IsLifted { get; set; }

    /// <summary>
    /// Whether this is a voluntary ban requested by user
    /// </summary>
    public bool IsVoluntary { get; set; }

    /// <summary>
    /// When the ban was lifted (if lifted early)
    /// </summary>
    public DateTimeOffset? LiftedUtc { get; set; }

    /// <summary>
    /// Who lifted the ban, by identifier. An id and not a user object: the row
    /// stores the moderator's id without a navigation to it, and inventing one
    /// would add a foreign key for a field the history does not print. It used to
    /// be a User the mapping explicitly ignored, so it was null on every ban.
    /// </summary>
    public Guid? LiftedByUserId { get; set; }

    /// <summary>
    /// Why the ban was lifted early
    /// </summary>
    public string? LiftReason { get; set; }
}

/// <summary>
/// Request to create a ban
/// </summary>
public class CreateBanRequest
{
    /// <summary>
    /// Target user username
    /// </summary>
    /// <example>problemuser</example>
    public string Username { get; set; } = "";

    /// <summary>
    /// Ban access restriction scope ("Тип бана" in the doc, 4.2.4.2):
    /// <see cref="AccessPolicy.DemocraticBan"/> silences public speech — the
    /// global chat, the forum, the discussion of other people's games and blogs —
    /// while the user's own games and blogs, posts in game rooms and direct
    /// messages stay open; <see cref="AccessPolicy.FullBan"/> blocks everything
    /// and fails authentication itself. This text is spelled out rather than
    /// linked because Swagger renders a cref as a bare type name, and a moderator
    /// picking the scope reads it here. Anything other than these two is refused
    /// with 400, an omitted scope included: a default here would answer a request
    /// that never named a scope with the strictest ban there is and report success.
    /// </summary>
    public AccessPolicy AccessPolicy { get; set; }

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
/// Ban as visible to everyone on the public profile
/// </summary>
/// <remarks>
/// Facts only — ban reason and moderator identity stay in the
/// moderation area (GET /v1/bans, /v1/moderation/users endpoints).
/// </remarks>
public class PublicBan
{
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
    /// Whether the ban is currently active
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// User ban status (public view)
/// </summary>
public class PublicUserBanStatus
{
    /// <summary>
    /// User username
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// Whether user is currently banned
    /// </summary>
    public bool IsBanned { get; set; }

    /// <summary>
    /// Active ban details (if banned), trimmed public view
    /// </summary>
    public PublicBan? ActiveBan { get; set; }

    /// <summary>
    /// Ban history (trimmed public view)
    /// </summary>
    public IEnumerable<PublicBan> History { get; set; } = Array.Empty<PublicBan>();
}

/// <summary>
/// User ban status (full view)
/// </summary>
/// <remarks>
/// Internal aggregation carrier for the moderation profile endpoints;
/// the public GET users/{username}/bans returns <see cref="PublicUserBanStatus"/>.
/// </remarks>
public class UserBanStatus
{
    /// <summary>
    /// User username
    /// </summary>
    public string Username { get; set; } = "";

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
