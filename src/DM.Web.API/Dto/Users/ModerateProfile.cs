namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO for moderating user profile (SeniorModerator+ only)
/// </summary>
public class ModerateProfile
{
    /// <summary>
    /// User-defined extended information (can be cleared by moderator)
    /// </summary>
    public string? Info { get; set; }
}
