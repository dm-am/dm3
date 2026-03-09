using System;

namespace DM.Domain.Moderation.Features.ProfileNotes;

/// <summary>
/// DTO for updating a moderator note
/// </summary>
public class UpdateModeratedProfileNote
{
    /// <summary>
    /// Note identifier
    /// </summary>
    public Guid NoteId { get; set; }

    /// <summary>
    /// New note text
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
