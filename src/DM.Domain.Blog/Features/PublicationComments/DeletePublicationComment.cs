using System;

namespace DM.Domain.Blog.Features.PublicationComments;

/// <summary>
/// DTO for deleting a publication comment
/// </summary>
public class DeletePublicationComment
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid CommentId { get; set; }

    /// <summary>
    /// Publication identifier
    /// </summary>
    public Guid PublicationId { get; set; }

    /// <summary>
    /// User who deleted the comment
    /// </summary>
    public Guid DeletedByUserId { get; set; }

    /// <summary>
    /// Deletion timestamp
    /// </summary>
    public DateTimeOffset DeletedUtc { get; set; }

    /// <summary>
    /// New comment count after deletion
    /// </summary>
    public int NewCommentCount { get; set; }

    /// <summary>
    /// New last comment ID (if this was the last comment)
    /// </summary>
    public Guid? NewLastCommentId { get; set; }
}
