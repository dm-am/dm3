using System;
using System.Collections.Generic;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <summary>
/// Warning DTO
/// </summary>
public class Warning
{
    /// <summary>
    /// Warning identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User who received the warning
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Moderator who issued the warning
    /// </summary>
    public User Moderator { get; set; } = null!;

    /// <summary>
    /// Warning causation entity ID
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Entity type (Comment, Message, etc.)
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Warning points (0-6, 0 = verbal warning without points)
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Warning reason/message
    /// </summary>
    public string Reason { get; set; } = "";

    /// <summary>
    /// The offending text as it stood when the warning was issued. Null when the
    /// warning names no content. Written once and never rewritten, so it survives
    /// both an edit and a deletion of the content it was taken from.
    /// </summary>
    public string? EntitySnapshot { get; set; }

    /// <summary>
    /// Site-relative address of the offending content. Null when the content is
    /// gone or has no page a moderator could open.
    /// </summary>
    public string? EntityUrl { get; set; }

    /// <summary>
    /// Whether the offending content was edited after the warning was issued —
    /// the snapshot and what stands there now are then different texts
    /// </summary>
    public bool EntityEditedAfterWarning { get; set; }

    /// <summary>
    /// Creation timestamp (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Whether the warning is still active
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Request to create a warning
/// </summary>
public class CreateWarningRequest
{
    /// <summary>
    /// Target user username
    /// </summary>
    /// <example>problemuser</example>
    public string Username { get; set; } = "";

    /// <summary>
    /// Entity ID that caused the warning
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Entity type (Comment, Message, Post, etc.)
    /// </summary>
    /// <example>Comment</example>
    public string? EntityType { get; set; }

    /// <summary>
    /// Warning points (0-6, 0 = verbal warning without points)
    /// </summary>
    /// <example>1</example>
    public int Points { get; set; } = 1;

    /// <summary>
    /// Warning reason/message
    /// </summary>
    /// <example>Rule violation: inappropriate content</example>
    public string Reason { get; set; } = "";
}

/// <summary>
/// Warning as visible to everyone on the public profile
/// </summary>
/// <remarks>
/// Aggregate facts only — reason, causation entity and moderator identity
/// stay in the moderation area (GET /v1/moderation/warnings).
/// </remarks>
public class PublicWarning
{
    /// <summary>
    /// Warning points (0-6, 0 = verbal warning without points)
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Creation timestamp (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Whether the warning is still active
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// User warnings summary (public view)
/// </summary>
public class UserWarningsInfo
{
    /// <summary>
    /// User username
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// Total active warning points
    /// </summary>
    public int TotalPoints { get; set; }

    /// <summary>
    /// Active warnings count
    /// </summary>
    public int ActiveCount { get; set; }

    /// <summary>
    /// List of warnings (trimmed public view)
    /// </summary>
    public IEnumerable<PublicWarning> Warnings { get; set; } = Array.Empty<PublicWarning>();
}
