namespace DM.Domain.Moderation.Features.Tags;

/// <summary>
/// DTO for creating a tag group
/// </summary>
public class CreateTagGroup
{
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
}
