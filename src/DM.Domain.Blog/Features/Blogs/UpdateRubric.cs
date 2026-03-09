using System;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// DTO for updating a rubric
/// </summary>
public class UpdateRubric
{
    /// <summary>
    /// Rubric identifier
    /// </summary>
    public Guid RubricId { get; set; }

    /// <summary>
    /// Rubric title (null = not changed)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Sort order (null = not changed)
    /// </summary>
    public int? SortOrder { get; set; }
}
