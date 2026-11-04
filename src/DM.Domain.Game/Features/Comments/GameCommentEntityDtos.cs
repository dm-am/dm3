using System;

namespace DM.Domain.Game.Features.Comments;

public class CreateGameCommentEntity
{
    public Guid CommentId { get; set; }

    public Guid GameId { get; set; }

    public Guid AuthorId { get; set; }

    public string Text { get; set; } = null!;

    public DateTimeOffset CreatedUtc { get; set; }
}

public class UpdateGameCommentEntity
{
    public Guid CommentId { get; set; }

    public string Text { get; set; } = null!;

    public DateTimeOffset ModifiedUtc { get; set; }

    public Guid EditorUserId { get; set; }
}

public class DeleteGameCommentEntity
{
    public Guid CommentId { get; set; }

    public Guid GameId { get; set; }

    public Guid DeletedByUserId { get; set; }

    public DateTimeOffset DeletedUtc { get; set; }
}
