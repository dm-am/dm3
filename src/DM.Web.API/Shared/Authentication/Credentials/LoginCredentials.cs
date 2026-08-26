namespace DM.Web.API.Shared.Authentication.Credentials;

/// <summary>
/// Login credentials for authentication
/// </summary>
public class LoginCredentials : AuthCredentials
{
    /// <summary>
    /// User email address
    /// </summary>
    /// <example>john@example.com</example>
    public string Email { get; set; } = "";

    /// <summary>
    /// User password
    /// </summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// Honeypot field for bot protection - must be left empty
    /// </summary>
    /// <remarks>
    /// This field is invisible to users but often filled by automated bots.
    /// If this field contains any value, the login attempt will be rejected.
    /// </remarks>
    public string? Website { get; set; }

    /// <summary>
    /// Keep the session past the browser being closed
    /// </summary>
    /// <remarks>
    /// True issues a persistent session cookie and the longer of the two
    /// configured lifetimes; false issues the ordinary one. Both are counted
    /// from the moment of login and extended when the session is used close to
    /// its expiry, so neither is a sliding window of inactivity.
    /// Defaults to true for convenience.
    /// </remarks>
    public bool RememberMe { get; set; } = true;
}
