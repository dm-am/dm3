using System;
using DM.Domain.Core.Comments;

namespace DM.Domain.Blog.Features.PublicationComments;

public class PublicationCommentToDelete : Comment
{
    public Guid PublicationId => EntityId;
}

public class UpdatePublicationCommentEntity
{
    public Guid CommentId { get; set; }

    public string Text { get; set; } = null!;

    public DateTimeOffset LastUpdateUtc { get; set; }

    public Guid EditorUserId { get; set; }
}

public class DeletePublicationCommentEntity
{
    public Guid CommentId { get; set; }

    public Guid PublicationId { get; set; }

    public Guid DeletedByUserId { get; set; }

    public DateTimeOffset DeletedUtc { get; set; }
}
