using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Games.Reading;
using DM.Services.Game.BusinessProcesses.Shared;
using DM.Services.Game.Dto.Output;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Game.BusinessProcesses.Unread;

/// <inheritdoc />
internal class FirstUnreadService : IFirstUnreadService
{
    private readonly DmDbContext _dbContext;
    private readonly IGameReadingService _gameReadingService;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;

    public FirstUnreadService(
        DmDbContext dbContext,
        IGameReadingService gameReadingService,
        IUnreadCountersRepository unreadCountersRepository,
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager)
    {
        _dbContext = dbContext;
        _gameReadingService = gameReadingService;
        _unreadCountersRepository = unreadCountersRepository;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
    }

    /// <inheritdoc />
    public async Task<FirstUnreadPostResult> GetFirstUnreadPost(Guid gameId)
    {
        var game = await _gameReadingService.GetGame(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Read, game);

        var userId = _identityProvider.Current.User.UserId;
        var isAuthenticated = _identityProvider.Current.User.IsAuthenticated;

        // Get all accessible rooms ordered by OrderNumber
        var rooms = await _dbContext.Rooms
            .Where(r => r.GameId == gameId)
            .Where(AccessibilityFilters.RoomAvailable(userId))
            .OrderBy(r => r.OrderNumber)
            .Select(r => new { r.RoomId, r.OrderNumber })
            .ToListAsync();

        if (!rooms.Any())
        {
            return new FirstUnreadPostResult { HasUnread = false };
        }

        var roomIds = rooms.Select(r => r.RoomId).ToArray();

        // For anonymous users - return first post of the first room
        if (!isAuthenticated)
        {
            return await GetFirstPostInRooms(roomIds);
        }

        // Get last read times for all rooms
        var lastReadTimes = await _unreadCountersRepository.GetLastReadTimes(
            userId, UnreadEntryType.Message, roomIds);

        // For each room, find the first unread post
        foreach (var roomId in roomIds)
        {
            var lastRead = lastReadTimes.TryGetValue(roomId, out var time)
                ? time
                : DateTime.MinValue;

            // Find first post created after last read time
            var firstUnread = await _dbContext.Posts
                .Where(p => p.RoomId == roomId && !p.IsRemoved)
                .Where(p => p.CreatedUtc > lastRead)
                .OrderBy(p => p.CreatedUtc)
                .Select(p => new { p.PostId, p.CreatedUtc })
                .FirstOrDefaultAsync();

            if (firstUnread != null)
            {
                // Count posts up to and including this one (1-based number)
                var postNumber = await _dbContext.Posts
                    .Where(p => p.RoomId == roomId && !p.IsRemoved)
                    .CountAsync(p => p.CreatedUtc <= firstUnread.CreatedUtc);

                // Count total unread posts in the game
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

        // No unread posts - return last post of the last room
        return await GetLastPostInRooms(roomIds);
    }

    /// <inheritdoc />
    public async Task<FirstUnreadCommentResult> GetFirstUnreadComment(Guid gameId)
    {
        var game = await _gameReadingService.GetGame(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.ReadComments, game);

        var userId = _identityProvider.Current.User.UserId;
        var isAuthenticated = _identityProvider.Current.User.IsAuthenticated;

        // For anonymous users - return first comment
        if (!isAuthenticated)
        {
            return await GetFirstComment(gameId);
        }

        // Get last read time for comments
        var lastRead = await _unreadCountersRepository.GetLastReadTime(
            userId, gameId, UnreadEntryType.Message) ?? DateTime.MinValue;

        // Find first comment created after last read time
        var firstUnread = await _dbContext.Comments
            .Where(c => c.EntityId == gameId && !c.IsRemoved)
            .Where(c => c.CreatedUtc > lastRead)
            .OrderBy(c => c.CreatedUtc)
            .Select(c => new { c.CommentId, c.CreatedUtc })
            .FirstOrDefaultAsync();

        if (firstUnread != null)
        {
            // Count comments up to and including this one (1-based number)
            var commentNumber = await _dbContext.Comments
                .Where(c => c.EntityId == gameId && !c.IsRemoved)
                .CountAsync(c => c.CreatedUtc <= firstUnread.CreatedUtc);

            // Count total unread comments
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

        // No unread comments - return last comment
        return await GetLastComment(gameId);
    }

    private async Task<FirstUnreadPostResult> GetFirstPostInRooms(Guid[] roomIds)
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

        return new FirstUnreadPostResult { HasUnread = false };
    }

    private async Task<FirstUnreadPostResult> GetLastPostInRooms(Guid[] roomIds)
    {
        // Find the last room with posts (iterate in reverse order)
        for (var i = roomIds.Length - 1; i >= 0; i--)
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
                    PostNumber = postCount, // Last post number
                    TotalUnreadCount = 0,
                    HasUnread = false
                };
            }
        }

        return new FirstUnreadPostResult { HasUnread = false };
    }

    private async Task<FirstUnreadCommentResult> GetFirstComment(Guid gameId)
    {
        var firstComment = await _dbContext.Comments
            .Where(c => c.EntityId == gameId && !c.IsRemoved)
            .OrderBy(c => c.CreatedUtc)
            .Select(c => new { c.CommentId })
            .FirstOrDefaultAsync();

        if (firstComment == null)
        {
            return new FirstUnreadCommentResult { HasUnread = false };
        }

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

    private async Task<FirstUnreadCommentResult> GetLastComment(Guid gameId)
    {
        var lastComment = await _dbContext.Comments
            .Where(c => c.EntityId == gameId && !c.IsRemoved)
            .OrderByDescending(c => c.CreatedUtc)
            .Select(c => new { c.CommentId })
            .FirstOrDefaultAsync();

        if (lastComment == null)
        {
            return new FirstUnreadCommentResult { HasUnread = false };
        }

        var commentCount = await _dbContext.Comments
            .Where(c => c.EntityId == gameId && !c.IsRemoved)
            .CountAsync();

        return new FirstUnreadCommentResult
        {
            CommentId = lastComment.CommentId,
            CommentNumber = commentCount, // Last comment number
            TotalUnreadCount = 0,
            HasUnread = false
        };
    }
}
