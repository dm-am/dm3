using System;
using System.Collections.Generic;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.BbRendering;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO model for user public profile (extends UserSummary with additional profile information)
/// </summary>
/// <remarks>
/// This class is kept for backward compatibility. For new code, prefer using:
/// - UserSummary for lists and minimal references
/// - User (this class) for public profile views
/// - UserDetails for owner's account view with settings
/// </remarks>
public class User : UserSummary
{
    /// <summary>
    /// Roles (kept for backward compatibility, prefer PrimaryRole)
    /// </summary>
    public IEnumerable<UserRole> Roles { get; set; } = Array.Empty<UserRole>();

    /// <summary>
    /// Newbie status (less than 100 posts)
    /// </summary>
    public bool IsNewbie { get; set; }

    /// <summary>
    /// User gender
    /// </summary>
    public Gender Gender { get; set; }

    /// <summary>
    /// User birthday information
    /// </summary>
    public UserBirthday? Birthday { get; set; }

    /// <summary>
    /// User registration moment (UTC)
    /// </summary>
    public DateTimeOffset? RegistrationDateUtc { get; set; }

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
    /// User contact information
    /// </summary>
    public IEnumerable<UserContact> Contacts { get; set; } = Array.Empty<UserContact>();

    /// <summary>
    /// User-defined extended information (BB-code rendered)
    /// </summary>
    public InfoBbText? Info { get; set; }

}

/// <summary>
/// Alias for UserRating (backward compatibility, prefer UserRating)
/// </summary>
public class Rating : UserRating
{
}
