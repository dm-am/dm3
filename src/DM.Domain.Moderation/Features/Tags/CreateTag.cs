using System;

namespace DM.Domain.Moderation.Features.Tags;

public class CreateTag
{
    public Guid GroupId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Sort order within group (lower values appear first)
    /// </summary>
    public int SortOrder { get; set; }
}
