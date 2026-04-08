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
    /// Warning points (1-3)
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Warning reason/message
    /// </summary>
    public string Reason { get; set; } = "";

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
    /// Warning points (1-3)
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
/// User warnings summary
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
    /// List of warnings
    /// </summary>
    public IEnumerable<Warning> Warnings { get; set; } = Array.Empty<Warning>();
}
