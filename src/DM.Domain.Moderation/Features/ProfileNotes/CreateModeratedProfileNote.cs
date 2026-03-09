namespace DM.Domain.Moderation.Features.ProfileNotes;

/// <summary>
/// DTO for creating a moderator note
/// </summary>
public class CreateModeratedProfileNote
{
    /// <summary>
    /// Username this note is about
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Note text
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
