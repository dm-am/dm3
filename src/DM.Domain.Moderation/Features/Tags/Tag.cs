using System;

namespace DM.Domain.Moderation.Features.Tags;

public class Tag
{
    public Guid Id { get; set; }

    /// <summary>
    /// Short numeric ID for URLs
    /// </summary>
    public int ShortId { get; set; }

    public Guid GroupId { get; set; }

    public string GroupTitle { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

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
