using System;

namespace DM.Domain.Moderation.Features.Tags;

/// <summary>
/// Domain model for tag
/// </summary>
public class Tag
{
    /// <summary>
    /// Tag ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Short numeric ID for URLs
    /// </summary>
    public int ShortId { get; set; }

    /// <summary>
    /// Tag group ID
    /// </summary>
    public Guid GroupId { get; set; }

    /// <summary>
    /// Tag group title
    /// </summary>
    public string GroupTitle { get; set; } = string.Empty;

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sort order within group (lower values appear first)
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Number of games using this tag
    /// </summary>
    public int GamesCount { get; set; }
}
