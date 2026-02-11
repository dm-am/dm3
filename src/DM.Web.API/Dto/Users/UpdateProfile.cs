using System;
using System.Collections.Generic;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO for updating user profile
/// </summary>
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
    /// User-defined extended information (BB-code)
    /// </summary>
    public string? Info { get; set; }

    /// <summary>
    /// User contacts (replaces all contacts when provided)
    /// </summary>
    public IReadOnlyCollection<UserContact>? Contacts { get; set; }

    /// <summary>
    /// Disable rating display
    /// </summary>
    public bool? RatingDisabled { get; set; }

    /// <summary>
    /// Upload ID for new avatar (from /v1/uploads)
    /// </summary>
    public Guid? AvatarUploadId { get; set; }
}
