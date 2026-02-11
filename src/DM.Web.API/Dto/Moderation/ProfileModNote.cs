using System;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Dto.Moderation;

/// <summary>
/// Moderator note about a user
/// </summary>
public class ProfileModNote
{
    /// <summary>
    /// Note identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User this note is about
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Moderator who created the note
    /// </summary>
    public User Author { get; set; } = null!;

    /// <summary>
    /// Note text
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Creation timestamp (UTC)
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Last modification timestamp (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedAtUtc { get; set; }
}

/// <summary>
/// Request to create a moderator note
/// </summary>
public class CreateProfileModNoteRequest
{
    /// <summary>
    /// Note text
    /// </summary>
    /// <example>User has been warned about spamming in the past</example>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Request to update a moderator note
/// </summary>
public class UpdateProfileModNoteRequest
{
    /// <summary>
    /// Updated note text
    /// </summary>
    /// <example>Updated note: User has improved their behavior</example>
    public string Text { get; set; } = string.Empty;
}
