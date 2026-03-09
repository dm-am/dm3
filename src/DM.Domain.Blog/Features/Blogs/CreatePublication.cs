using System;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// DTO for creating a publication
/// </summary>
public class CreatePublication
{
    /// <summary>
    /// Parent blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Rubric identifier (optional)
    /// </summary>
    public Guid? RubricId { get; set; }

    /// <summary>
    /// Publication title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Publication content
    /// </summary>
    public string Content { get; set; } = null!;

    /// <summary>
    /// Short preview/excerpt
    /// </summary>
    public string Preview { get; set; } = null!;

    /// <summary>
    /// Whether to publish immediately
    /// </summary>
    public bool PublishImmediately { get; set; }

    /// <summary>
    /// Whether comments are enabled
    /// </summary>
    public bool CommentsEnabled { get; set; } = true;
}
