using System;
using DM.Services.Core.Dto;

namespace DM.Services.Community.BusinessProcesses.Users.ModNotes;

/// <summary>
/// Moderator note about a user
/// </summary>
public class ProfileModNoteDto
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
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Last modification date (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedAtUtc { get; set; }
}
