using System.Collections.Generic;
using DM.Services.Authentication.Dto;
using DM.Services.Community.BusinessProcesses.Users.Reading;

namespace DM.Services.Community.BusinessProcesses.Users.Updating;

/// <summary>
/// DTO for user updating
/// </summary>
public class UpdateUser
{
    /// <summary>
    /// User login
    /// </summary>
    public string Login { get; set; } = null!;

    /// <summary>
    /// User defined status
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Rating disability flag
    /// </summary>
    public bool? RatingDisabled { get; set; }

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
    public IReadOnlyCollection<UserContactDto> Contacts { get; set; } = [];

    /// <summary>
    /// User settings
    /// </summary>
    public UserSettings Settings { get; set; } = null!;

    /// <summary>
    /// Upload ID for new avatar (from presigned URL upload flow)
    /// </summary>
    public System.Guid? AvatarUploadId { get; set; }
}