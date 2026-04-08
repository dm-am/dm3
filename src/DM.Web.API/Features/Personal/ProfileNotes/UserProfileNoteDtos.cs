using System;

namespace DM.Web.API.Features.Personal.ProfileNotes;

/// <summary>
/// User profile note - personal note about another user
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
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Note text content
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Creation date (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last update date (UTC)
    /// </summary>
    public DateTimeOffset? UpdatedUtc { get; set; }
}

/// <summary>
/// Request to create or update a user profile note
/// </summary>
public class UserProfileNoteRequest
{
    /// <summary>
    /// Note text content (max 2000 символов)
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
