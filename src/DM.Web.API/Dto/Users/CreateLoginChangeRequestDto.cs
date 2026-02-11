namespace DM.Web.API.Dto.Users;

/// <summary>
/// API DTO for creating a login change request
/// </summary>
public class CreateLoginChangeRequestDto
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
