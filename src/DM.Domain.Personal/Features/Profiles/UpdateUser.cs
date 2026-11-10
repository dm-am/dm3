using System;
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
    public string? Status { get; set; }

    /// <summary>
    /// Rating disability flag
    /// </summary>
    public bool? RatingDisabled { get; set; }

    /// <summary>
    /// Show birthday to other users
    /// </summary>
    public bool? ShowBirthday { get; set; }

    public string? Name { get; set; }

    /// <summary>
    /// User real location
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// User-defined extended information
    /// </summary>
    public string? Info { get; set; }

    /// <summary>
    /// User contacts: a list replaces the entire contact list, null leaves it
    /// as it is, like the fields above
    /// </summary>
    public IReadOnlyCollection<UserContact>? Contacts { get; set; }

    /// <summary>
    /// User settings
    /// </summary>
    public UserSettings? Settings { get; set; }

    /// <summary>
    /// Upload ID for new avatar (from presigned URL upload flow)
    /// </summary>
    public Guid? AvatarUploadId { get; set; }
}
