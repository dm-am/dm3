using System;

namespace DM.Services.Community.BusinessProcesses.Blogs.Writing;

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
    /// Whether the blog is public (null = not changed)
    /// </summary>
    public bool? IsPublic { get; set; }

    /// <summary>
    /// Whether comments are enabled (null = not changed)
    /// </summary>
    public bool? CommentsEnabled { get; set; }
}
