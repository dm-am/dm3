using System;
using System.Linq;
using System.Linq.Expressions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Shared.Queries;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Infrastructure.Persistence.Shared.Comments;

/// <summary>
/// The order a page of comments is read in, for all four discussions at once.
/// </summary>
/// <remarks>
/// Every branch ends on the identifier: neither a timestamp nor a like count is unique,
/// and paging needs the same order on every page.
/// </remarks>
internal static class CommentSorting
{
    /// <summary>
    /// Order <paramref name="query" /> by the field the reader asked for.
    /// </summary>
    /// <param name="query">Comments already narrowed to one discussion.</param>
    /// <param name="sortBy">"likes"; anything else, null included, orders by creation.</param>
    /// <param name="sortOrder">"desc" for descending, anything else for ascending.</param>
    /// <param name="dbContext">Context the like counts are read through.</param>
    public static IOrderedQueryable<DbComment> Apply(
        IQueryable<DbComment> query,
        string? sortBy,
        string? sortOrder,
        DmDbContext dbContext)
    {
        var isDescending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        var ordered = string.Equals(sortBy, "likes", StringComparison.OrdinalIgnoreCase)
            ? query.OrderByDirection(LiveLikes(dbContext), !isDescending)
            : query.OrderByDirection(c => c.CreatedUtc, !isDescending);

        return ordered.ThenBy(c => c.CommentId);
    }

    /// <summary>
    /// Likes of the comment counted in SQL, so the order and the number the page
    /// renders come from one count.
    /// </summary>
    private static Expression<Func<DbComment, int>> LiveLikes(DmDbContext dbContext) =>
        comment => dbContext.Likes.Count(like =>
            !like.IsRemoved &&
            like.EntityId == comment.CommentId &&
            like.EntityType == LikeEntityType.Comment);
}
