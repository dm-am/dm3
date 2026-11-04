namespace DM.Domain.Moderation.Features.ProfileNotes;

public class CreateModeratedProfileNote
{
    /// <summary>
    /// Username this note is about
    /// </summary>
    public string Username { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
