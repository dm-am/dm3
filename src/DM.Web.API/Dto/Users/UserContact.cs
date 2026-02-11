namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO model for user contact information
/// </summary>
public class UserContact
{
    /// <summary>
    /// Contact type title (e.g., "Telegram", "Discord", "Email")
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Contact value (e.g., username, email address)
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
