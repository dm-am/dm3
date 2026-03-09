using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Unread;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// Repository for finding first unread posts and comments in games
/// </summary>
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
            .OrderBy(r => r.OrderNumber)
            .Select(r => r.RoomId)
            .ToListAsync();
    }

    public async Task<FirstUnreadPostResult?> FindFirstUnreadPost(
        IReadOnlyList<Guid> roomIds,
        IDictionary<Guid, DateTime> lastReadTimes)
    {
        foreach (var roomId in roomIds)
        {
            var lastRead = lastReadTimes.TryGetValue(roomId, out var time)
                ? time
                : DateTime.MinValue;

            var firstUnread = await _dbContext.Posts
                .Where(p => p.RoomId == roomId && !p.IsRemoved)
                .Where(p => p.CreatedUtc > lastRead)
                .OrderBy(p => p.CreatedUtc)
                .Select(p => new { p.PostId, p.CreatedUtc })
                .FirstOrDefaultAsync();

            if (firstUnread != null)
            {
                var postNumber = await _dbContext.Posts
                    .Where(p => p.RoomId == roomId && !p.IsRemoved)
                    .CountAsync(p => p.CreatedUtc <= firstUnread.CreatedUtc);

                var totalUnread = 0;
                foreach (var rid in roomIds)
                {
                    var roomLastRead = lastReadTimes.TryGetValue(rid, out var t)
                        ? t
                        : DateTime.MinValue;
                    totalUnread += await _dbContext.Posts
                        .Where(p => p.RoomId == rid && !p.IsRemoved)
                        .CountAsync(p => p.CreatedUtc > roomLastRead);
                }

                return new FirstUnreadPostResult
                {
                    RoomId = roomId,
                    PostId = firstUnread.PostId,
                    PostNumber = postNumber,
                    TotalUnreadCount = totalUnread,
                    HasUnread = true
                };
            }
        }

        return null;
    }

    public async Task<FirstUnreadPostResult?> GetFirstPostInRooms(IReadOnlyList<Guid> roomIds)
    {
        foreach (var roomId in roomIds)
        {
            var firstPost = await _dbContext.Posts
                .Where(p => p.RoomId == roomId && !p.IsRemoved)
                .OrderBy(p => p.CreatedUtc)
                .Select(p => new { p.PostId })
                .FirstOrDefaultAsync();

            if (firstPost != null)
            {
                var totalCount = 0;
                foreach (var rid in roomIds)
                {
                    totalCount += await _dbContext.Posts
                        .Where(p => p.RoomId == rid && !p.IsRemoved)
                        .CountAsync();
                }

                return new FirstUnreadPostResult
                {
                    RoomId = roomId,
                    PostId = firstPost.PostId,
                    PostNumber = 1,
                    TotalUnreadCount = totalCount,
                    HasUnread = true
                };
            }
        }

        return null;
    }

    public async Task<FirstUnreadPostResult?> GetLastPostInRooms(IReadOnlyList<Guid> roomIds)
    {
        for (var i = roomIds.Count - 1; i >= 0; i--)
        {
            var roomId = roomIds[i];
            var lastPost = await _dbContext.Posts
                .Where(p => p.RoomId == roomId && !p.IsRemoved)
                .OrderByDescending(p => p.CreatedUtc)
                .Select(p => new { p.PostId })
                .FirstOrDefaultAsync();

            if (lastPost != null)
            {
                var postCount = await _dbContext.Posts
                    .Where(p => p.RoomId == roomId && !p.IsRemoved)
                    .CountAsync();

                return new FirstUnreadPostResult
                {
                    RoomId = roomId,
                    PostId = lastPost.PostId,
                    PostNumber = postCount,
                    TotalUnreadCount = 0,
                    HasUnread = false
                };
            }
        }

        return null;
    }

    public async Task<FirstUnreadCommentResult?> FindFirstUnreadComment(Guid gameId, DateTime lastRead)
    {
        var firstUnread = await _dbContext.Comments
            .Where(c => c.EntityId == gameId && !c.IsRemoved)
            .Where(c => c.CreatedUtc > lastRead)
            .OrderBy(c => c.CreatedUtc)
            .Select(c => new { c.CommentId, c.CreatedUtc })
            .FirstOrDefaultAsync();

        if (firstUnread == null)
            return null;

        var commentNumber = await _dbContext.Comments
            .Where(c => c.EntityId == gameId && !c.IsRemoved)
            .CountAsync(c => c.CreatedUtc <= firstUnread.CreatedUtc);

        var totalUnread = await _dbContext.Comments
            .Where(c => c.EntityId == gameId && !c.IsRemoved)
            .CountAsync(c => c.CreatedUtc > lastRead);

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
        var firstComment = await _dbContext.Comments
            .Where(c => c.EntityId == gameId && !c.IsRemoved)
            .OrderBy(c => c.CreatedUtc)
            .Select(c => new { c.CommentId })
            .FirstOrDefaultAsync();

        if (firstComment == null)
            return null;

        var totalCount = await _dbContext.Comments
            .Where(c => c.EntityId == gameId && !c.IsRemoved)
            .CountAsync();

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
        var lastComment = await _dbContext.Comments
            .Where(c => c.EntityId == gameId && !c.IsRemoved)
            .OrderByDescending(c => c.CreatedUtc)
            .Select(c => new { c.CommentId })
            .FirstOrDefaultAsync();

        if (lastComment == null)
            return null;

        var commentCount = await _dbContext.Comments
            .Where(c => c.EntityId == gameId && !c.IsRemoved)
            .CountAsync();

        return new FirstUnreadCommentResult
        {
            CommentId = lastComment.CommentId,
            CommentNumber = commentCount,
            TotalUnreadCount = 0,
            HasUnread = false
        };
    }
}
