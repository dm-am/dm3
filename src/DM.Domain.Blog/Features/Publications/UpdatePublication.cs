using System;

namespace DM.Domain.Blog.Features.Publications;

/// <summary>
/// DTO for updating a publication
/// </summary>
public class UpdatePublication
{
    /// <summary>
    /// Publication identifier
    /// </summary>
    public Guid PublicationId { get; set; }

    /// <summary>
    /// Rubric identifier (null = not changed)
    /// </summary>
    public Guid? RubricId { get; set; }

    /// <summary>
    /// Whether to clear the rubric
    /// </summary>
    public bool ClearRubric { get; set; }

    /// <summary>
    /// Publication title (null = not changed)
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Publication content (null = not changed)
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// Short preview/excerpt (null = not changed)
    /// </summary>
    public string Preview { get; set; } = null!;

    /// <summary>
    /// Whether the publication is published (null = not changed)
    /// </summary>
    public bool? IsPublished { get; set; }

    /// <summary>
    /// Whether comments are enabled (null = not changed)
    /// </summary>
    public bool? CommentsEnabled { get; set; }
}
