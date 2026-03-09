namespace DM.Web.API.Features.Personal.Blacklists;

/// <summary>
/// Request to block a user
/// </summary>
public class BlockUserRequest
{
    /// <summary>
    /// Username of the user to block
    /// </summary>
    public string Username { get; set; } = string.Empty;
}
