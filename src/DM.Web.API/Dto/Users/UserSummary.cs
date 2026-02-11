using System;
using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// Minimal user DTO for lists and references
/// </summary>
public class UserSummary
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User login (unique username)
    /// </summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>
    /// Primary user role
    /// </summary>
    public UserRole PrimaryRole { get; set; }

    /// <summary>
    /// Honorary goblin status (special title for distinguished users)
    /// </summary>
    public bool IsHonorary { get; set; }

    /// <summary>
    /// User access policy (ban/restriction status)
    /// </summary>
    public AccessPolicy AccessPolicy { get; set; }

    /// <summary>
    /// Last activity moment (UTC). Updated on any API request.
    /// </summary>
    public DateTimeOffset? LastActivityUtc { get; set; }

    /// <summary>
    /// User rating information
    /// </summary>
    public UserRating Rating { get; set; } = new();

    /// <summary>
    /// User profile picture (SmallUrl only in lists)
    /// </summary>
    public UserPicture Picture { get; set; } = new();
}
