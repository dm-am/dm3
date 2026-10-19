namespace DM.Web.API.Shared.Authentication.Credentials;

/// <summary>
/// Login credentials for authentication; the fields mean what they mean in LoginRequest
/// </summary>
public class LoginCredentials : AuthCredentials
{
    public string Email { get; set; } = "";

    public string Password { get; set; } = "";

    public bool RememberMe { get; set; } = true;
}
