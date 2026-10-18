using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence.Shared.Likes;
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
    /// Identifiers of every game, removed ones included: the owners a game
    /// comment can hang on. Removal of the game is the discussion's own answer
    /// and is asked where the discussion is read, not here. The three sets below
    /// are the same for blogs, publications and topics.
    /// </summary>
    public static IQueryable<Guid> GameIds(DmDbContext dbContext) =>
        dbContext.Games.IgnoreQueryFilters().Select(g => g.GameId);

    /// <summary>
    /// Identifiers of every blog, removed ones included (see <see cref="GameIds" />).
    /// </summary>
    public static IQueryable<Guid> BlogIds(DmDbContext dbContext) =>
        dbContext.Blogs.IgnoreQueryFilters().Select(b => b.BlogId);

    /// <summary>
    /// Identifiers of every publication, removed ones included (see <see cref="GameIds" />).
    /// </summary>
    public static IQueryable<Guid> PublicationIds(DmDbContext dbContext) =>
        dbContext.Publications.IgnoreQueryFilters().Select(p => p.PublicationId);

    /// <summary>
    /// Identifiers of every topic, removed ones included (see <see cref="GameIds" />).
    /// </summary>
    public static IQueryable<Guid> TopicIds(DmDbContext dbContext) =>
        dbContext.Topics.IgnoreQueryFilters().Select(t => t.TopicId);

    /// <summary>
    /// The live comment with this identifier, if it hangs on one of the owners.
    /// </summary>
    /// <remarks>
    /// Shared by the read of a comment and by the read before its removal, so a
    /// route removes only what it can read (see <see cref="Single" />).
    /// </remarks>
    public static IQueryable<DbComment> One(DmDbContext dbContext, Guid commentId, IQueryable<Guid> owners) =>
        dbContext.Comments.Where(c => !c.IsRemoved && c.CommentId == commentId && owners.Contains(c.EntityId));

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
            // The page lifts the account filter to keep the comments of removed
            // accounts (RemovedAuthors); a removed login matches nothing here, or
            // the old name would find what the removal took off it.
            var authorNames = commentsQuery.AuthorUsernames.Select(a => a.ToLowerInvariant()).ToArray();
            query = query.Where(c => c.Author != null && !c.Author.IsRemoved &&
                                     authorNames.Contains(c.Author.Username.ToLower()));
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

        var page = await CommentSorting
            .Apply(filtered, commentsQuery.SortBy, commentsQuery.SortOrder, dbContext)
            .Page(paging)
            .ProjectToComment()
            .ToArrayAsync(ct);

        await FillLikes(dbContext, page, queryTag, ct);
        return page;
    }

    /// <summary>
    /// A single live comment by identifier, among the comments of one kind of
    /// discussion.
    /// </summary>
    /// <remarks>
    /// The kind is the caller's: <paramref name="owners" /> is one of the id sets
    /// below. The table has no discriminator, and a read by the identifier alone
    /// answered every route with every comment - the edit route of a game took a
    /// blog comment, asked the game's rules about it, none of which apply to a
    /// blog, and saved it. So a comment of another kind is not there to a route,
    /// exactly as a comment that does not exist.
    /// </remarks>
    public static async Task<Comment?> Single(
        DmDbContext dbContext,
        Guid commentId,
        IQueryable<Guid> owners,
        string queryTag,
        CancellationToken ct = default)
    {
        var comment = await One(dbContext, commentId, owners)
            .TagWith(queryTag)
            .ProjectToComment()
            .FirstOrDefaultAsync(ct);

        if (comment is not null)
        {
            await FillLikes(dbContext, [comment], queryTag, ct);
        }

        return comment;
    }

    /// <summary>
    /// Backfill <see cref="Comment.Likes"/> for comments already read.
    /// </summary>
    /// <remarks>
    /// The projection cannot carry them (see <see cref="LikeBackfill" />), and every
    /// comment read answered with an empty list: nobody saw how many likes a comment
    /// had, and the author of a like saw an empty heart after a reload, so the click
    /// meant to take the like back asked for a second one and was refused.
    ///
    /// The like tags are derived from the caller's so they still name the module.
    /// </remarks>
    public static Task FillLikes(
        DmDbContext dbContext,
        IReadOnlyCollection<Comment> comments,
        string queryTag,
        CancellationToken ct = default) =>
        LikeBackfill.Fill(dbContext, comments, LikeEntityType.Comment,
            c => c.Id, (c, likes) => c.Likes = likes,
            $"{queryTag}.Likes", $"{queryTag}.Likers", ct);

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
