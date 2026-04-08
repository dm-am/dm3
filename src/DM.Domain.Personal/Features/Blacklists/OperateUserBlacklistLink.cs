namespace DM.Domain.Personal.Features.Blacklists;

/// <summary>
/// DTO for user blacklist operations
/// </summary>
public class OperateUserBlacklistLink
{
    /// <summary>
    /// User's display name (unique username)
    /// </summary>
    public string Username { get; set; } = null!;
}
