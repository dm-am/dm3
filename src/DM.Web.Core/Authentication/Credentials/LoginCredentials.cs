namespace DM.Web.Core.Authentication.Credentials;

/// <summary>
/// Login-password credentials
/// </summary>
public class LoginCredentials : AuthCredentials
{
    /// <summary>
    /// Login
    /// </summary>
    public string Login { get; set; }

    /// <summary>
    /// Password
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Flag to create persistent session
    /// </summary>
    public bool RememberMe { get; set; }

    /// <summary>
    /// Honeypot field for bot protection (should be empty)
    /// </summary>
    public string Website { get; set; }
}