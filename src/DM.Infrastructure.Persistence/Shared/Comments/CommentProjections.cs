using System;
using System.Linq;
using System.Linq.Expressions;
using DM.Domain.Core.Comments;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Infrastructure.Persistence.Shared.Comments;

/// <summary>
/// The one projection for the comment pair. Forum, game, blog and
/// publication comments are the same entity and the same DTO, so a second
/// formula in a module mapper would specialise nothing - it would shadow
/// this one.
/// </summary>
internal static class CommentProjections
{
    /// <summary>
    /// The comment members, written once. The edit history is the
    /// modification time: the entity keeps every edit instead of overwriting
    /// one column, so "edited at" is the most recent of them. Likes are
    /// fetched separately via the EntityType+EntityId pattern.
    /// </summary>
    private static readonly Expression<Func<DbComment, Comment>> RowFields =
        c => new Comment
        {
            Id = c.CommentId,
            EntityId = c.EntityId,
            CreatedUtc = c.CreatedUtc,
            ModifiedUtc = c.Edits
                .OrderByDescending(e => e.EditedUtc)
                .Select(e => (DateTimeOffset?)e.EditedUtc)
                .FirstOrDefault(),
            Text = c.Text,
            Author = GeneralUserProjections.Projection.Splice(c.Author)
        };

    /// <summary>
    /// EF projection to the domain comment.
    /// </summary>
    public static IQueryable<Comment> ProjectToComment(this IQueryable<DbComment> query) =>
        query.Select(ExpressionSplicer.Expand(RowFields));

    /// <summary>
    /// The same members over a delete-path DTO of one module: every one of
    /// them derives from <see cref="Comment"/> and adds counters the
    /// repository fills afterwards, so the module only names what it appends.
    /// </summary>
    public static Expression<Func<DbComment, TTarget>> WithCommentFields<TTarget>(
        Expression<Func<DbComment, TTarget>> targetFields)
        where TTarget : Comment =>
        ExpressionSplicer.WithBaseBindings(RowFields, targetFields);
}
