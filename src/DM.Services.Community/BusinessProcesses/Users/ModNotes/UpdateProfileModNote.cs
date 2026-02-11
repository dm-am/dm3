using System;

namespace DM.Services.Community.BusinessProcesses.Users.ModNotes;

/// <summary>
/// DTO for updating a moderator note
/// </summary>
public class UpdateProfileModNote
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
