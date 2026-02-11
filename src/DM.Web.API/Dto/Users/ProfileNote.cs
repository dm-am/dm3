using System;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// Profile note - personal note about another user
/// </summary>
public class ProfileNote
{
    /// <summary>
    /// Note identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Subject user login (who the note is about)
    /// </summary>
    public string UserLogin { get; set; } = string.Empty;

    /// <summary>
    /// Note text content
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Creation date
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Last update date
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// Request to create or update a profile note
/// </summary>
public class ProfileNoteRequest
{
    /// <summary>
    /// Note text content (max 2000 characters)
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
