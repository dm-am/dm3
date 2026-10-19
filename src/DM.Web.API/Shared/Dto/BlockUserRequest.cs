namespace DM.Web.API.Shared.Dto;

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
