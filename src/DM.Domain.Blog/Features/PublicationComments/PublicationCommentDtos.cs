using System;
using DM.Domain.Core.Comments;

namespace DM.Domain.Blog.Features.PublicationComments;

/// <summary>
/// DTO for publication comment ready for deletion
/// </summary>
public class PublicationCommentToDelete : Comment
{
    /// <summary>
    /// Publication identifier
    /// </summary>
    public Guid PublicationId => EntityId;

    /// <summary>
    /// Current comment count of the publication
    /// </summary>
    public int PublicationCommentCount { get; set; }

    /// <summary>
    /// Tells if the comment is last comment of the publication
    /// </summary>
    public bool IsLastComment { get; set; }
}

/// <summary>
/// Entity DTO for updating a publication comment (repository level)
/// </summary>
public class UpdatePublicationCommentEntity
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
/// Entity DTO for deleting a publication comment (repository level)
/// </summary>
public class DeletePublicationCommentEntity
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
