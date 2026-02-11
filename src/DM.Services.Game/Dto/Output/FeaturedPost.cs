using System;
using DM.Services.Core.Dto;

namespace DM.Services.Game.Dto.Output;

/// <summary>
/// DTO model for featured post (best of week or last with plus)
/// </summary>
public class FeaturedPost
{
    /// <summary>
    /// Post identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Post text preview (plain text, max 300 chars)
    /// </summary>
    public string TextPreview { get; set; } = string.Empty;

    /// <summary>
    /// Post author
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Character name (if post was written as character)
    /// </summary>
    public string? CharacterName { get; set; }

    /// <summary>
    /// Post creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Game title
    /// </summary>
    public string GameTitle { get; set; } = string.Empty;

    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Room title
    /// </summary>
    public string RoomTitle { get; set; } = string.Empty;

    /// <summary>
    /// Sum of review SignValues (rating)
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Number of reviews on this post
    /// </summary>
    public int ReviewCount { get; set; }
}
