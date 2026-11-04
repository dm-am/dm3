using System;
using DM.Domain.Core.Comments;

namespace DM.Domain.Blog.Features.Comments;

public class BlogCommentToDelete : Comment
{
    public Guid BlogId => EntityId;
}

public class UpdateBlogCommentEntity
{
    public Guid CommentId { get; set; }

    public string Text { get; set; } = null!;

    public DateTimeOffset LastUpdateUtc { get; set; }

    public Guid EditorUserId { get; set; }
}

public class DeleteBlogCommentEntity
{
    public Guid CommentId { get; set; }

    public Guid BlogId { get; set; }

    public Guid DeletedByUserId { get; set; }

    public DateTimeOffset DeletedUtc { get; set; }
}
