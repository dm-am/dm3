using System;

namespace DM.Domain.Personal.Features.ProfileNotes;

/// <summary>
/// User profile note DTO
/// </summary>
public class UserProfileNote
{
    /// <summary>
    /// Note identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Subject username (who the note is about)
    /// </summary>
    public string SubjectUsername { get; set; } = string.Empty;

    /// <summary>
    /// Subject user ID
    /// </summary>
    public Guid SubjectUserId { get; set; }

    /// <summary>
    /// Note text
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Creation date
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last update date
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }
}

/// <summary>
/// Entity DTO for writing a user profile note, new or existing (repository level)
/// </summary>
public class CreateUserProfileNoteEntity
{
    /// <summary>
    /// Note identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Owner identifier (who wrote the note)
    /// </summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// Subject user identifier (who the note is about)
    /// </summary>
    public Guid SubjectUserId { get; set; }

    /// <summary>
    /// Note text
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Moment of the write: the creation date of a new note, the modification
    /// date of one the pair already has
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
