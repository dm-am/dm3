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
using BoardEntity = DM.Infrastructure.Persistence.Entities.Forum.Board;
using TopicEntity = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <summary>
/// Projection formulas for the forum DTOs.
///
/// The board shape is one spliced expression, asked for on its own and nested
/// under a topic. It used to be two blocks "that must stay identical".
///
/// The user entries under a board are deliberately narrow GeneralUser stubs
/// (name, role, rating), not the full <see cref="GeneralUserProjections"/>
/// formula: the board list renders moderator names only, and the full row per
/// moderator per board would be paid on every forum index render. The stub is
/// a formula of its own for that reason - so narrowing it stays a decision
/// somebody made once, rather than four hand-written member lists that agree
/// by luck.
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
    private static readonly Expression<Func<BoardEntity, Board>> BoardProjection =
        b => new Board
        {
            Id = b.BoardId,
            Title = b.Title,
            Alias = b.Alias,
            Description = b.Description!,
            CreateTopicPolicy = b.CreateTopicPolicy,
            ViewPolicy = b.ViewPolicy,
            TopicsCount = b.TopicsCount,
            ModeratorIds = b.Moderators.Select(m => m.UserId).ToList(),
            Moderators = b.Moderators
                .Select(m => BoardUserStub.Splice(m.User))
                .ToList(),
            LastTopic = b.LastTopicId.HasValue
                ? new BoardLastTopic
                {
                    Id = b.LastTopicId.Value,
                    TopicNumber = b.LastTopicNumber ?? 0,
                    Title = b.LastTopicTitle ?? string.Empty,
                    CreatedUtc = b.LastTopicCreatedUtc!.Value,
                    Author = b.LastTopicAuthor != null
                        ? BoardUserStub.Splice(b.LastTopicAuthor)
                        : null
                }
                : null
        };

    /// <summary>EF projection to the domain board</summary>
    public static IQueryable<Board> ProjectToBoard(this IQueryable<BoardEntity> query) =>
        query.Select(ExpressionSplicer.Expand(BoardProjection));

    /// <summary>
    /// EF projection to the domain topic.
    ///
    /// Performance note (PERFORMANCE.md: "Avoid inline aggregations"):
    /// TotalCommentsCount and its unread sibling are intentionally absent
    /// here. Counting via <c>t.Comments.Count()</c> inside the Select would
    /// generate one correlated <c>SELECT COUNT(*)</c> per row; the repository
    /// fills them in a single batched <c>GROUP BY</c> pass after the main
    /// projection, and Likes/LikesCount follow the same rule.
    /// </summary>
    public static IQueryable<Topic> ProjectToTopic(this IQueryable<TopicEntity> query) =>
        query.Select(ExpressionSplicer.Expand<Func<TopicEntity, Topic>>(t => new Topic
        {
            Id = t.TopicId,
            TopicNumber = t.TopicNumber,
            Title = t.Title,
            Text = t.Text,
            CreatedUtc = t.CreatedUtc,
            IsAttached = t.IsAttached,
            AttachOrder = t.AttachOrder,
            IsClosed = t.IsClosed,
            LastActivityUtc = t.LastComment == null ? t.CreatedUtc : t.LastComment.CreatedUtc,
            Author = GeneralUserProjections.Projection.Splice(t.Author),
            // The last comment keeps its default identifier on purpose - the
            // AutoMapper map never carried it, and no consumer reads it.
            LastComment = t.LastComment == null
                ? null!
                : new LastComment
                {
                    CreatedUtc = t.LastComment.CreatedUtc,
                    Author = GeneralUserProjections.Projection.Splice(t.LastComment.Author)
                },
            Board = BoardProjection.Splice(t.Board)
        }));

    /// <summary>
    /// EF projection for the delete path: the comment shape (see
    /// <see cref="Shared.Comments.CommentProjections"/>) into the derived
    /// DTO; IsLastComment is set by the repository afterwards.
    /// </summary>
    public static IQueryable<TopicCommentToDelete> ProjectToTopicCommentToDelete(
        this IQueryable<DbComment> query) =>
        query.Select(ExpressionSplicer.Expand(
            CommentProjections.WithCommentFields<TopicCommentToDelete>(c => new TopicCommentToDelete())));
}
