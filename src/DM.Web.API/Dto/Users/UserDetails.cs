using System;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO model for user account (private view with settings)
/// This class represents the full account information available only to the account owner
/// </summary>
public class UserDetails : User
{
    /// <summary>
    /// User email address (only visible to account owner)
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Given post review text (user's default review text)
    /// </summary>
    public string? GivenPostReview { get; set; }

    /// <summary>
    /// User settings (only visible to account owner)
    /// </summary>
    public UserSettings Settings { get; set; } = new();
}
