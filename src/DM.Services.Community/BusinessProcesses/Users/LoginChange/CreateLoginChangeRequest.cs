namespace DM.Services.Community.BusinessProcesses.Users.LoginChange;

/// <summary>
/// DTO for creating a login change request
/// </summary>
public class CreateLoginChangeRequest
{
    /// <summary>
    /// Desired new login
    /// </summary>
    public string RequestedLogin { get; set; } = null!;

    /// <summary>
    /// Reason for the change
    /// </summary>
    public string Reason { get; set; } = null!;
}
