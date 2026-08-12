using System;
using System.Linq;
using System.Linq.Expressions;
using DM.Domain.Core.Enums;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Infrastructure.Persistence.Shared.Comments;

/// <summary>
/// The order a page of comments is read in, for all four discussions at once.
/// </summary>
/// <remarks>
/// Forum topics, games, blogs and publications keep their comments in one table, and each
/// of the four repositories carried a byte-identical copy of this switch. A fix to the
/// order therefore had to be made four times, which is the shape a fix takes right before
/// it gets made three.
///
/// Every branch ends on the identifier, and that is what the class is for. Neither key
/// above it is unique: a busy second gives several comments one timestamp, and a like
/// count ties by design. Paging asks for the same order once per page, and rows the order
/// cannot tell apart may come back arranged differently between two of those asks, which
/// shows one of them on both pages and the other on neither.
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

        var ordered = sortBy?.ToLowerInvariant() switch
        {
            "likes" => isDescending
                ? query.OrderByDescending(LiveLikes(dbContext))
                : query.OrderBy(LiveLikes(dbContext)),
            _ => isDescending // "created" or default
                ? query.OrderByDescending(c => c.CreatedUtc)
                : query.OrderBy(c => c.CreatedUtc)
        };

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
