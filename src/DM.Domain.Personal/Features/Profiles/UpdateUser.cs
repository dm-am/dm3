using System.Collections.Generic;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;

namespace DM.Domain.Personal.Features.Profiles;

/// <summary>
/// DTO for user updating
/// </summary>
public class UpdateUser
{
    /// <summary>
    /// User's display name (unique username)
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>
    /// User defined status
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Rating disability flag
    /// </summary>
    public bool? RatingDisabled { get; set; }

    /// <summary>
    /// Show birthday to other users
    /// </summary>
    public bool? ShowBirthday { get; set; }

    /// <summary>
    /// User real name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// User real location
    /// </summary>
    public string Location { get; set; } = null!;

    /// <summary>
    /// User-defined extended information
    /// </summary>
    public string Info { get; set; } = null!;

    /// <summary>
    /// User contacts (replaces entire contact list when provided)
    /// </summary>
    public IReadOnlyCollection<UserContact> Contacts { get; set; } = [];

    /// <summary>
    /// User settings
    /// </summary>
    public UserSettings Settings { get; set; } = null!;

    /// <summary>
    /// Upload ID for new avatar (from presigned URL upload flow)
    /// </summary>
    public System.Guid? AvatarUploadId { get; set; }
}