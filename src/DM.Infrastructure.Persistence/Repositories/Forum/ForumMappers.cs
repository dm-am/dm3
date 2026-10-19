using System;
using System.Linq;
using System.Linq.Expressions;
using DM.Domain.Core.Dto;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Forum.Features.Comments;
using DM.Domain.Forum.Features.Topics;
using DM.Infrastructure.Persistence.Shared.Comments;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using DbBoard = DM.Infrastructure.Persistence.Entities.Forum.Board;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <summary>
/// Projection formulas for the forum DTOs. The board shape is one spliced
/// expression, asked for on its own and nested under a topic.
/// </summary>
internal static class ForumMappers
{
    /// <summary>
    /// The narrow user stub the board list renders: everything the full
    /// <see cref="GeneralUserProjections.Projection"/> would add is paid per
    /// moderator per board and shown nowhere.
    /// </summary>
    internal static readonly Expression<Func<DbUser, GeneralUser>> BoardUserStub =
        u => new GeneralUser
        {
            UserId = u.UserId,
            Username = u.Username,
            Role = u.Role,
            Status = u.Status,
            QuantityRating = u.QuantityRating
        };

    /// <summary>
    /// The one board formula. UnreadTopicsCount/UnreadCommentsCount are filled
    /// per reader after the query; CommentsCount and LastComment are not
    /// columns any more - they are computed on read in BoardRepository, so a
    /// counter can never be stale.
    /// </summary>
    private static readonly Expression<Func<DbBoard, Board>> BoardProjection =
        b => new Board
        {
            Id = b.BoardId,
            Title = b.Title,
            Alias = b.Alias,
            Description = b.Description!,
            CreateTopicPolicy = b.CreateTopicPolicy,
            TopicsCount = b.TopicsCount,
            ModeratorIds = b.Moderators.Select(m => m.UserId).ToList(),
            // Spelled out rather than left to the account filter: the board is
            // nested under a topic, and the topic read lifts that filter to keep
            // what removed accounts wrote (see RemovedAuthors). A removed
            // moderator moderates nothing and is named nowhere.
            Moderators = b.Moderators
                .Where(m => !m.User.IsRemoved)
                .Select(m => BoardUserStub.Splice(m.User))
                .ToList(),
            LastTopic = b.LastTopicId.HasValue
                ? new BoardLastTopic
                {
                    Id = b.LastTopicId.Value,
                    TopicNumber = b.LastTopicNumber ?? 0,
                    Title = b.LastTopicTitle ?? string.Empty,
                    CreatedUtc = b.LastTopicCreatedUtc!.Value,
                    Author = b.LastTopicAuthor != null && !b.LastTopicAuthor.IsRemoved
                        ? BoardUserStub.Splice(b.LastTopicAuthor)
                        : null
                }
                : null
        };

    /// <summary>EF projection to the domain board</summary>
    public static IQueryable<Board> ProjectToBoard(this IQueryable<DbBoard> query) =>
        query.Select(ExpressionSplicer.Expand(BoardProjection));

    /// <summary>
    /// EF projection to the domain topic. Comment and like counts are filled by the
    /// repository in one GROUP BY: inside the Select they would be a COUNT per row.
    /// Keeps the topics of removed accounts, signed by nobody (<see cref="RemovedAuthors"/>).
    /// </summary>
    public static IQueryable<Topic> ProjectToTopic(this IQueryable<DbTopic> query) =>
        query.KeepingRemovedAuthors().Select(ExpressionSplicer.Expand<Func<DbTopic, Topic>>(t => new Topic
        {
            Id = t.TopicId,
            TopicNumber = t.TopicNumber,
            Title = t.Title,
            Text = t.Text,
            CreatedUtc = t.CreatedUtc,
            IsAttached = t.IsAttached,
            AttachOrder = t.AttachOrder,
            IsClosed = t.IsClosed,
            ClosedByAuthor = t.ClosedByAuthor,
            LastActivityUtc = t.LastComment == null ? t.CreatedUtc : t.LastComment.CreatedUtc,
            Author = GeneralUserProjections.Projection.Splice(t.Author),
            // The identifier is what the topic lists link to (#comment-<id>):
            // left at its default, every "last comment" link led nowhere.
            LastComment = t.LastComment == null
                ? null!
                : new LastComment
                {
                    Id = t.LastComment.CommentId,
                    CreatedUtc = t.LastComment.CreatedUtc,
                    Author = GeneralUserProjections.Projection.Splice(t.LastComment.Author)
                },
            Board = BoardProjection.Splice(t.Board)
        }));

    /// <summary>
    /// EF projection for the delete path: the comment shape (see
    /// <see cref="Shared.Comments.CommentProjections"/>) into the derived
    /// DTO, which adds nothing to it but a name for the topic identifier.
    /// </summary>
    public static IQueryable<TopicCommentToDelete> ProjectToTopicCommentToDelete(
        this IQueryable<DbComment> query) =>
        query.KeepingRemovedAuthors().Select(ExpressionSplicer.Expand(
            CommentProjections.WithCommentFields<TopicCommentToDelete>(c => new TopicCommentToDelete())));
}
