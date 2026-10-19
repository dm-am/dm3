using System;

namespace DM.Domain.Moderation.Features.Tags;

public class TagGroup
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Sort order (lower values appear first)
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// How many tags of this group one game may carry; null means no limit
    /// </summary>
    public int? MaxTagsPerGame { get; set; }

    /// <summary>
    /// Number of tags in this group
    /// </summary>
    public int TagsCount { get; set; }
}
