namespace DM.Web.API.Shared.Authentication.Credentials;

/// <summary>
/// Token credentials
/// </summary>
public class TokenCredentials : AuthCredentials
{
    /// <summary>
    /// Token
    /// </summary>
    public required string Token { get; set; }
}