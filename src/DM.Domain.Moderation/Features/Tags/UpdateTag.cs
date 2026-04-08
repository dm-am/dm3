using System;

namespace DM.Domain.Moderation.Features.Tags;

/// <summary>
/// DTO for updating a tag
/// </summary>
public class UpdateTag
{
    /// <summary>
    /// Tag ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tag group ID
    /// </summary>
    public Guid GroupId { get; set; }

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
}
