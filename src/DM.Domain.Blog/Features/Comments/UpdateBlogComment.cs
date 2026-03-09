using System;

namespace DM.Domain.Blog.Features.Comments;

/// <summary>
/// DTO for updating a blog comment
/// </summary>
public class UpdateBlogComment
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid CommentId { get; set; }

    /// <summary>
    /// Updated comment text
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Last edit timestamp
    /// </summary>
    public DateTimeOffset LastUpdateUtc { get; set; }
}
