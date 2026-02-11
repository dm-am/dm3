namespace DM.Services.Community.BusinessProcesses.Blogs.Writing;

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
    /// Whether the blog is public
    /// </summary>
    public bool IsPublic { get; set; } = true;

    /// <summary>
    /// Whether comments are enabled
    /// </summary>
    public bool CommentsEnabled { get; set; } = true;
}
