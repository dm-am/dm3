using System;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Unified lightweight user reference for lists, tooltips, and participant info.
/// Replaces: User in lists, GameAssistantInfo, BlogAssistantInfo
/// Only 5 fields vs 12+ in full User
/// </summary>
/// <remarks>
/// Use this DTO for:
/// - Game lists (master, mentor, assistants, players, readers)
/// - Blog lists (owner, assistants)
/// - Any context where only identity and online status are needed
///
/// For full user information, use User DTO from Community/Users.
/// </remarks>
public class UserRef
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User's display name
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Last activity moment (UTC) - for online indicators
    /// </summary>
    public DateTimeOffset? LastActivityUtc { get; set; }

    /// <summary>
    /// User role (for displaying role badges "[А]", "[С]", "[М]", "[Н]", "[Р]")
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Whether user is a newbie (less than 100 posts) - affects name color
    /// </summary>
    public bool IsNewbie { get; set; }
}
