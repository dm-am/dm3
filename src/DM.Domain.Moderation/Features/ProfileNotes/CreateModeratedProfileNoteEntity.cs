using System;

namespace DM.Domain.Moderation.Features.ProfileNotes;

public class CreateModeratedProfileNoteEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// User the note is about
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Moderator who wrote the note
    /// </summary>
    public Guid AuthorId { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }
}
