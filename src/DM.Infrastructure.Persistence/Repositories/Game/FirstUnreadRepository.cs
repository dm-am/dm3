using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Unread;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Game;

internal class FirstUnreadRepository : IFirstUnreadRepository
{
    private readonly DmDbContext _dbContext;

    public FirstUnreadRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Guid>> GetAccessibleRoomIds(Guid gameId, Guid userId)
    {
        return await _dbContext.Rooms
            .Where(r => r.GameId == gameId)
            .Where(GameAccessibilityFilters.RoomAvailable(userId))
            .InGameOrder()
            .Select(r => r.RoomId)
            .ToListAsync();
    }

    public async Task<FirstUnreadPostResult?> FindFirstUnreadPost(
        IReadOnlyList<Guid> roomIds,
        IDictionary<Guid, DateTime> lastReadTimes)
    {
        var summaries = await RoomSummaries(
            roomIds,
            roomId => lastReadTimes.TryGetValue(roomId, out var time) ? time : DateTime.MinValue);

        // The order is the caller's: GetAccessibleRoomIds sorts rooms as they are shown.
        var room = FirstHolding(roomIds, summaries);
        if (room == null)
        {
            return null;
        }

        var postId = await PostAt(room.RoomId, room.FirstUtc);

        var postNumber = await _dbContext.Posts
            .Where(p => p.RoomId == room.RoomId)
            .CountAsync(p => p.CreatedUtc <= room.FirstUtc);

        return new FirstUnreadPostResult
        {
            RoomId = room.RoomId,
            PostId = postId,
            PostNumber = postNumber,
            TotalUnreadCount = summaries.Values.Sum(s => s.Count),
            HasUnread = true
        };
    }

    public async Task<FirstUnreadPostResult?> GetFirstPostInRooms(IReadOnlyList<Guid> roomIds)
    {
        // With no lower bound every post counts: the first room and the total of the game.
        var summaries = await RoomSummaries(roomIds, _ => DateTime.MinValue);

        var room = FirstHolding(roomIds, summaries);
        if (room == null)
        {
            return null;
        }

        return new FirstUnreadPostResult
        {
            RoomId = room.RoomId,
            PostId = await PostAt(room.RoomId, room.FirstUtc),
            PostNumber = 1,
            TotalUnreadCount = summaries.Values.Sum(s => s.Count),
            HasUnread = true
        };
    }

    public async Task<FirstUnreadPostResult?> GetLastPostInRooms(IReadOnlyList<Guid> roomIds)
    {
        var summaries = await RoomSummaries(roomIds, _ => DateTime.MinValue);

        // A reader with nothing unread is put down in the last room that holds anything.
        var room = FirstHolding(roomIds.Reverse(), summaries);
        if (room == null)
        {
            return null;
        }

        return new FirstUnreadPostResult
        {
            RoomId = room.RoomId,
            PostId = await PostAt(room.RoomId, room.LastUtc),
            PostNumber = room.Count,
            TotalUnreadCount = 0,
            HasUnread = false
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// The unread posts are the tail of the room: they are the ones written after a
    /// moment, and the room is ordered by the moment first. So the first of them
    /// stands right after the read ones, and its number is the count of those plus
    /// one — ties on the timestamp included, since a tie is read or unread as a
    /// whole. Ordered by (CreatedUtc, PostId) like the page it opens, so the post
    /// named is the one the page shows at that number.
    /// </remarks>
    public async Task<FirstUnreadRoomPostResult> LocateInRoom(Guid roomId, DateTime lastRead)
    {
        var from = Utc(lastRead);
        var posts = _dbContext.Posts
            .TagWith("DM.Game.RoomFirstUnread")
            .Where(p => p.RoomId == roomId);

        // Both counts in one statement: taken apart, a post written between them
        // would move the number by one and could put the reader on the page before.
        var counts = await posts
            .GroupBy(p => p.RoomId)
            .Select(g => new { Total = g.Count(), Unread = g.Count(p => p.CreatedUtc > from) })
            .FirstOrDefaultAsync();
        var total = counts?.Total ?? 0;
        var unread = counts?.Unread ?? 0;

        if (unread > 0)
        {
            var firstUnread = await posts
                .Where(p => p.CreatedUtc > from)
                .OrderBy(p => p.CreatedUtc)
                .ThenBy(p => p.PostId)
                .Select(p => p.PostId)
                .FirstAsync();

            return new FirstUnreadRoomPostResult
            {
                PostId = firstUnread,
                PostNumber = total - unread + 1,
                TotalUnreadCount = unread,
                HasUnread = true
            };
        }

        // Everything is read: the reader is put down at the end of the room.
        var last = total == 0
            ? null
            : await posts
                .OrderByDescending(p => p.CreatedUtc)
                .ThenByDescending(p => p.PostId)
                .Select(p => (Guid?)p.PostId)
                .FirstAsync();

        return new FirstUnreadRoomPostResult
        {
            PostId = last,
            PostNumber = total,
            TotalUnreadCount = 0,
            HasUnread = false
        };
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetLastOwnPostUtc(Guid roomId, Guid userId)
    {
        var last = await _dbContext.Posts
            .TagWith("DM.Game.RoomLastOwnPost")
            .Where(p => p.RoomId == roomId && p.AuthorId == userId)
            .MaxAsync(p => (DateTimeOffset?)p.CreatedUtc);

        return last?.UtcDateTime;
    }

    /// <summary>
    /// One row per room that holds a post past its own lower bound.
    /// </summary>
    /// <remarks>
    /// A room per query, and a second pass over every room to total them up, cost
    /// 2N+1 round trips to open a game — and this runs on every "go to first
    /// unread". The bound differs per room, so the rooms are asked as a union of
    /// per-room aggregates: one statement, and the database groups it.
    /// </remarks>
    /// <param name="roomIds">Rooms to summarise</param>
    /// <param name="lowerBound">Moment each room is counted from, exclusive</param>
    private async Task<Dictionary<Guid, RoomPostSummary>> RoomSummaries(
        IReadOnlyList<Guid> roomIds,
        Func<Guid, DateTime> lowerBound)
    {
        IQueryable<RoomPostSummary>? union = null;

        foreach (var roomId in roomIds)
        {
            var from = Utc(lowerBound(roomId));
            var part = _dbContext.Posts
                .Where(p => p.RoomId == roomId && p.CreatedUtc > from)
                .GroupBy(p => p.RoomId)
                .Select(g => new RoomPostSummary
                {
                    RoomId = g.Key,
                    Count = g.Count(),
                    FirstUtc = g.Min(p => p.CreatedUtc),
                    LastUtc = g.Max(p => p.CreatedUtc)
                });

            union = union == null ? part : union.Concat(part);
        }

        if (union == null)
        {
            return [];
        }

        var rows = await union.ToListAsync();
        return rows.ToDictionary(row => row.RoomId);
    }

    private static RoomPostSummary? FirstHolding(
        IEnumerable<Guid> roomIds, Dictionary<Guid, RoomPostSummary> summaries) =>
        roomIds.Select(id => summaries.GetValueOrDefault(id)).FirstOrDefault(summary => summary != null);

    private Task<Guid> PostAt(Guid roomId, DateTimeOffset createdUtc) => _dbContext.Posts
        .Where(p => p.RoomId == roomId && p.CreatedUtc == createdUtc)
        .Select(p => p.PostId)
        .FirstAsync();

    /// <summary>
    /// The moments arrive as UTC DateTime; the implicit conversion would read an unspecified
    /// Kind as local time, which throws for DateTime.MinValue east of UTC.
    /// </summary>
    private static DateTimeOffset Utc(DateTime moment) =>
        new(DateTime.SpecifyKind(moment, DateTimeKind.Utc));

    private IQueryable<Entities.Shared.Comment> GameComments(Guid gameId) =>
        _dbContext.Comments.Where(c => c.EntityId == gameId);

    private sealed class RoomPostSummary
    {
        public Guid RoomId { get; init; }
        public int Count { get; init; }
        public DateTimeOffset FirstUtc { get; init; }
        public DateTimeOffset LastUtc { get; init; }
    }

    public async Task<FirstUnreadCommentResult?> FindFirstUnreadComment(Guid gameId, DateTime lastRead)
    {
        var from = Utc(lastRead);
        var firstUnread = await GameComments(gameId)
            .Where(c => c.CreatedUtc > from)
            .OrderBy(c => c.CreatedUtc)
            .Select(c => new { c.CommentId, c.CreatedUtc })
            .FirstOrDefaultAsync();

        if (firstUnread == null)
            return null;

        var commentNumber = await GameComments(gameId)
            .CountAsync(c => c.CreatedUtc <= firstUnread.CreatedUtc);

        var totalUnread = await GameComments(gameId)
            .CountAsync(c => c.CreatedUtc > from);

        return new FirstUnreadCommentResult
        {
            CommentId = firstUnread.CommentId,
            CommentNumber = commentNumber,
            TotalUnreadCount = totalUnread,
            HasUnread = true
        };
    }

    public async Task<FirstUnreadCommentResult?> GetFirstComment(Guid gameId)
    {
        var firstComment = await GameComments(gameId)
            .OrderBy(c => c.CreatedUtc)
            .Select(c => new { c.CommentId })
            .FirstOrDefaultAsync();

        if (firstComment == null)
            return null;

        var totalCount = await GameComments(gameId).CountAsync();

        return new FirstUnreadCommentResult
        {
            CommentId = firstComment.CommentId,
            CommentNumber = 1,
            TotalUnreadCount = totalCount,
            HasUnread = true
        };
    }

    public async Task<FirstUnreadCommentResult?> GetLastComment(Guid gameId)
    {
        var lastComment = await GameComments(gameId)
            .OrderByDescending(c => c.CreatedUtc)
            .Select(c => new { c.CommentId })
            .FirstOrDefaultAsync();

        if (lastComment == null)
            return null;

        var commentCount = await GameComments(gameId).CountAsync();

        return new FirstUnreadCommentResult
        {
            CommentId = lastComment.CommentId,
            CommentNumber = commentCount,
            TotalUnreadCount = 0,
            HasUnread = false
        };
    }
}
