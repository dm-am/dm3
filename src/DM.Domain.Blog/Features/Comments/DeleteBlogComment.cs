using System;

namespace DM.Domain.Blog.Features.Comments;

/// <summary>
/// DTO for deleting a blog comment
/// </summary>
public class DeleteBlogComment
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid CommentId { get; set; }

    /// <summary>
    /// Blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// User who deleted the comment
    /// </summary>
    public Guid DeletedByUserId { get; set; }

    /// <summary>
    /// Deletion timestamp
    /// </summary>
    public DateTimeOffset DeletedAtUtc { get; set; }

    /// <summary>
    /// New comment count after deletion
    /// </summary>
    public int NewCommentCount { get; set; }

    /// <summary>
    /// New last comment ID (if this was the last comment)
    /// </summary>
    public Guid? NewLastCommentId { get; set; }
}
