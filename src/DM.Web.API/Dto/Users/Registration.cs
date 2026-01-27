namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO model for user registration
/// </summary>
public class Registration
{
    /// <summary>
    /// Login
    /// </summary>
    public string Login { get; set; }

    /// <summary>
    /// Email
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Password
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Honeypot field for bot protection (should be empty)
    /// </summary>
    public string? Website { get; set; }
}