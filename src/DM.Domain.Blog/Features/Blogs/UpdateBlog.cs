using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// DTO for updating a blog
/// </summary>
public class UpdateBlog
{
    /// <summary>
    /// Blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Blog title (null = not changed)
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Blog description (null = not changed)
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Draft visibility (null = not changed)
    /// </summary>
    public DraftVisibility? DraftVisibility { get; set; }

    /// <summary>
    /// Whether comments are enabled (null = not changed)
    /// </summary>
    public bool? CommentsEnabled { get; set; }
}
