namespace DM.Services.Community.BusinessProcesses.Users.ModNotes;

/// <summary>
/// DTO for creating a moderator note
/// </summary>
public class CreateProfileModNote
{
    /// <summary>
    /// User login this note is about
    /// </summary>
    public string UserLogin { get; set; } = string.Empty;

    /// <summary>
    /// Note text
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
