namespace DM.Domain.Personal.Features.ProfileNotes;

/// <summary>
/// Input DTO for creating or updating a profile note
/// </summary>
public class CreateUserProfileNote
{
    /// <summary>
    /// Username of the subject of the note
    /// </summary>
    public string SubjectUsername { get; set; } = null!;

    /// <summary>
    /// Note text
    /// </summary>
    public string Text { get; set; } = null!;
}
