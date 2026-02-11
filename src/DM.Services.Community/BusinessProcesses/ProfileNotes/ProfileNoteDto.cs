using System;

namespace DM.Services.Community.BusinessProcesses.ProfileNotes;

/// <summary>
/// Profile note DTO
/// </summary>
public class ProfileNoteDto
{
    /// <summary>
    /// Note identifier
    /// </summary>
    public Guid NoteId { get; set; }

    /// <summary>
    /// Subject user login (who the note is about)
    /// </summary>
    public string SubjectUserLogin { get; set; } = string.Empty;

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
    public DateTimeOffset? UpdatedUtc { get; set; }
}

/// <summary>
/// Create profile note request
/// </summary>
public class CreateProfileNote
{
    /// <summary>
    /// Subject user login (who the note is about)
    /// </summary>
    public string SubjectUserLogin { get; set; } = string.Empty;

    /// <summary>
    /// Note text
    /// </summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Update profile note request
/// </summary>
public class UpdateProfileNote
{
    /// <summary>
    /// Note text
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
