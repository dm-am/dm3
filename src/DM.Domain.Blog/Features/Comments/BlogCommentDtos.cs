using System;
using DM.Domain.Core.Comments;

namespace DM.Domain.Blog.Features.Comments;

/// <summary>
/// DTO for blog comment ready for deletion
/// </summary>
public class BlogCommentToDelete : Comment
{
    /// <summary>
    /// Blog identifier
    /// </summary>
    public Guid BlogId => EntityId;

    /// <summary>
    /// Current comment count of the blog
    /// </summary>
    public int BlogCommentCount { get; set; }

    /// <summary>
    /// Tells if the comment is last comment of the blog
    /// </summary>
    public bool IsLastComment { get; set; }
}

/// <summary>
/// Entity DTO for updating a blog comment (repository level)
/// </summary>
public class UpdateBlogCommentEntity
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

/// <summary>
/// Entity DTO for deleting a blog comment (repository level)
/// </summary>
public class DeleteBlogCommentEntity
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
