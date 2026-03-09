namespace DM.Domain.Account.Features.UsernameChange;

/// <summary>
/// DTO for creating a username change request
/// </summary>
public class CreateUsernameChangeRequest
{
    /// <summary>
    /// Reason for the change (username is chosen after moderator approval)
    /// </summary>
    public string Reason { get; set; } = null!;
}
