using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Moderation.Features.ProfileNotes;

/// <summary>
/// Moderator note about a user
/// </summary>
public class ModeratedProfileNote
{
    /// <summary>
    /// Note identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User this note is about
    /// </summary>
    public GeneralUser User { get; set; } = null!;

    /// <summary>
    /// Moderator who created this note
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Note text
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Creation date (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification date (UTC)
    /// </summary>
    public DateTimeOffset? UpdatedUtc { get; set; }
}

/// <summary>
/// Entity DTO for creating a moderated profile note (repository level)
/// </summary>
public class CreateModeratedProfileNoteEntity
{
    /// <summary>
    /// Note identifier
    /// </summary>
    public Guid NoteId { get; set; }

    /// <summary>
    /// User identifier (who the note is about)
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Author identifier (moderator who wrote the note)
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Note text
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Entity DTO for updating a moderated profile note (repository level)
/// </summary>
public class UpdateModeratedProfileNoteEntity
{
    /// <summary>
    /// Note identifier
    /// </summary>
    public Guid NoteId { get; set; }

    /// <summary>
    /// Note text
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Update timestamp
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}
