using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Infrastructure.Persistence.Shared.Comments;

/// <summary>
/// The reading side of a discussion, for all four discussions at once.
/// </summary>
/// <remarks>
/// Forum topics, games, blogs and publications keep their comments in one table, and each
/// of the four repositories carried its own copy of the filter block, of the paged read and
/// of the two single-row reads. The copies were identical where it counts and had already
/// begun to drift where it does not, which is what a copy does on its way to becoming four
/// different answers to one question: the tie-break of the order (see <see cref="CommentSorting" />)
/// had to be made in four places, and so would every filter added to the list from here on.
///
/// The query tag stays with the caller. It is what names the module a slow query came from,
/// and one shared tag would take that away.
/// </remarks>
internal static class CommentQueries
{
    /// <summary>
    /// Live comments of one discussion, whichever kind of entity it hangs on.
    /// </summary>
    public static IQueryable<DbComment> Discussion(DmDbContext dbContext, Guid entityId) =>
        dbContext.Comments.Where(c => !c.IsRemoved && c.EntityId == entityId);

    /// <summary>
    /// Drop the comments of the authors this reader does not see.
    /// </summary>
    public static IQueryable<DbComment> WithoutAuthors(
        IQueryable<DbComment> query, IReadOnlyCollection<Guid>? excludeUserIds) =>
        excludeUserIds is { Count: > 0 }
            ? query.Where(c => !excludeUserIds.Contains(c.AuthorId))
            : query;

    /// <summary>
    /// Everything the reader asked to narrow the discussion by. Shared by the count and
    /// the listing so that a new filter reaches pagination and page alike.
    /// </summary>
    public static IQueryable<DbComment> ApplyFilters(
        IQueryable<DbComment> query,
        ICommentsQuery commentsQuery,
        IReadOnlyCollection<Guid>? excludeUserIds)
    {
        // Exclude blocked users
        query = WithoutAuthors(query, excludeUserIds);

        // Filter by authors (OR logic)
        if (commentsQuery.AuthorUsernames is { Count: > 0 })
        {
            var authorNames = commentsQuery.AuthorUsernames.Select(a => a.ToLowerInvariant()).ToArray();
            query = query.Where(c => c.Author != null && authorNames.Contains(c.Author.Username.ToLower()));
        }

        // Filter by created date range
        if (commentsQuery.CreatedFromUtc.HasValue)
        {
            query = query.Where(c => c.CreatedUtc >= commentsQuery.CreatedFromUtc.Value);
        }

        if (commentsQuery.CreatedToUtc.HasValue)
        {
            query = query.WhereAtOrBefore(c => c.CreatedUtc, commentsQuery.CreatedToUtc.Value);
        }

        // Search by text content
        if (!string.IsNullOrWhiteSpace(commentsQuery.Search))
        {
            var searchLower = commentsQuery.Search.ToLowerInvariant();
            query = query.Where(c => c.Text.ToLower().Contains(searchLower));
        }

        return query;
    }

    /// <summary>
    /// How many comments the filtered discussion holds.
    /// </summary>
    public static Task<int> Count(
        DmDbContext dbContext,
        Guid entityId,
        ICommentsQuery commentsQuery,
        IReadOnlyCollection<Guid>? excludeUserIds,
        string queryTag,
        CancellationToken ct = default) =>
        ApplyFilters(Discussion(dbContext, entityId).TagWith(queryTag), commentsQuery, excludeUserIds)
            .CountAsync(ct);

    /// <summary>
    /// One page of the filtered discussion, in the order the reader asked for.
    /// </summary>
    public static async Task<IEnumerable<Comment>> Page(
        DmDbContext dbContext,
        Guid entityId,
        ICommentsQuery commentsQuery,
        PagingData paging,
        IReadOnlyCollection<Guid>? excludeUserIds,
        string queryTag,
        CancellationToken ct = default)
    {
        var filtered = ApplyFilters(
            Discussion(dbContext, entityId).TagWith(queryTag), commentsQuery, excludeUserIds);

        return await CommentSorting
            .Apply(filtered, commentsQuery.SortBy, commentsQuery.SortOrder, dbContext)
            .Page(paging)
            .ProjectToComment()
            .ToArrayAsync(ct);
    }

    /// <summary>
    /// A single live comment by identifier, whichever discussion it belongs to.
    /// </summary>
    public static Task<Comment?> Single(
        DmDbContext dbContext,
        Guid commentId,
        string queryTag,
        CancellationToken ct = default) =>
        dbContext.Comments
            .TagWith(queryTag)
            .Where(c => !c.IsRemoved && c.CommentId == commentId)
            .ProjectToComment()
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// The newest live comment of the discussion other than the given one: the row the
    /// discussion's own last-comment pointer moves to when that one goes away. Ordered
    /// by the identifier among equal timestamps, for the reason
    /// <see cref="CommentSorting" /> spells out.
    /// </summary>
    public static Task<Guid?> NewestExcept(
        DmDbContext dbContext,
        Guid entityId,
        Guid exceptCommentId,
        string queryTag,
        CancellationToken ct = default) =>
        Discussion(dbContext, entityId)
            .TagWith(queryTag)
            .Where(c => c.CommentId != exceptCommentId)
            .OrderByDescending(c => c.CreatedUtc)
            .ThenByDescending(c => c.CommentId)
            .Select(c => (Guid?)c.CommentId)
            .FirstOrDefaultAsync(ct);
}
