using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Personal.Profiles;

/// <summary>
/// Own profile DTO for account owner
/// </summary>
/// <remarks>
/// Used for: GET /v1/users/me/profile
/// Extends UserProfile with private data (email, visibility settings).
/// Only the account owner can access this.
/// </remarks>
public class PersonalProfile : UserProfile
{
    /// <summary>
    /// User email address (only visible to account owner)
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's full birthday (always includes year if set)
    /// </summary>
    /// <remarks>
    /// Uses 'new' to hide UserProfile.Birthday for different mapping:
    /// - UserProfile: respects showBirthday privacy setting (null if hidden)
    /// - PersonalProfile: always shows full birthday to account owner
    ///
    /// Visibility for others is controlled via Visibility.ShowBirthday.
    /// </remarks>
    public new Birthday? Birthday { get; set; }

    /// <summary>
    /// Visibility settings (only visible to account owner)
    /// </summary>
    public VisibilitySettings Visibility { get; set; } = new();
}

/// <summary>
/// User visibility settings controlling what information is visible to other users
/// </summary>
/// <remarks>
/// Part of PersonalProfile - only the account owner sees these settings.
/// </remarks>
public class VisibilitySettings
{
    /// <summary>
    /// Whether birthday is visible to other users
    /// </summary>
    /// <remarks>
    /// If false, birthday will be null in UserProfile for other users.
    /// </remarks>
    public bool ShowBirthday { get; set; }

    /// <summary>
    /// Whether rating is visible in lists and cards
    /// </summary>
    /// <remarks>
    /// If false, rating will be null in User DTO (lists, post.author, etc.).
    /// In UserProfile rating is always shown regardless of this setting.
    /// </remarks>
    public bool ShowRating { get; set; } = true;
}

/// <summary>
/// DTO for updating user profile (PATCH /v1/users/me/profile)
/// </summary>
/// <remarks>
/// All fields are optional - PATCH updates only provided fields.
/// </remarks>
public class UpdateProfile
{
    /// <summary>
    /// User-defined status message
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// User real name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// User location
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// User gender
    /// </summary>
    public Gender? Gender { get; set; }

    /// <summary>
    /// User birthday
    /// </summary>
    /// <remarks>
    /// Pass full object { day, month, year } or null to remove.
    /// Partial update not supported.
    /// </remarks>
    public Birthday? Birthday { get; set; }

    /// <summary>
    /// User-defined extended information (BB-code)
    /// </summary>
    public string? Info { get; set; }

    /// <summary>
    /// User contacts
    /// </summary>
    /// <remarks>
    /// Replaces all contacts when provided. Array order = display order.
    /// Pass [] to remove all contacts.
    /// </remarks>
    public IReadOnlyCollection<Contact>? Contacts { get; set; }

    /// <summary>
    /// Upload ID for new avatar (from /v1/uploads)
    /// </summary>
    /// <remarks>
    /// Pass null to remove avatar.
    /// </remarks>
    public Guid? AvatarUploadId { get; set; }

    /// <summary>
    /// Visibility settings
    /// </summary>
    public UpdateVisibilitySettings? Visibility { get; set; }
}

/// <summary>
/// Visibility settings for PATCH update
/// </summary>
/// <remarks>
/// All fields are optional - only provided fields are updated.
/// </remarks>
public class UpdateVisibilitySettings
{
    /// <summary>
    /// Whether birthday is visible to other users
    /// </summary>
    public bool? ShowBirthday { get; set; }

    /// <summary>
    /// Whether rating is visible in lists and cards
    /// </summary>
    public bool? ShowRating { get; set; }
}
