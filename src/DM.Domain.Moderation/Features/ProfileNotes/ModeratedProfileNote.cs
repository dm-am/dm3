using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Moderation.Features.ProfileNotes;

/// <summary>
/// Moderator note about a user
/// </summary>
public class ModeratedProfileNote
{
    public Guid Id { get; set; }

    /// <summary>
    /// User this note is about
    /// </summary>
    public GeneralUser User { get; set; } = null!;

    /// <summary>
    /// Moderator who created this note
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }
}
