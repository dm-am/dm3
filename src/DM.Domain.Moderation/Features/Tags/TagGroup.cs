using System;

namespace DM.Domain.Moderation.Features.Tags;

/// <summary>
/// Domain model for tag group
/// </summary>
public class TagGroup
{
    /// <summary>
    /// Tag group ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sort order (lower values appear first)
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Number of tags in this group
    /// </summary>
    public int TagsCount { get; set; }
}
