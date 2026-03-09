using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Blogs;

/// <summary>
/// DTO for creating a blog
/// </summary>
public class CreateBlog
{
    /// <summary>
    /// Blog title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Blog description
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Draft visibility (Private = only roles, Public = preview visible to all)
    /// </summary>
    public DraftVisibility DraftVisibility { get; set; } = DraftVisibility.Public;

    /// <summary>
    /// Whether comments are enabled
    /// </summary>
    public bool CommentsEnabled { get; set; } = true;

    /// <summary>
    /// Copy personal blacklist to blog blacklist on creation
    /// </summary>
    public bool CopyBlacklist { get; set; }
}
