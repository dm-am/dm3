using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Authorization;

namespace DM.Domain.Game.Features.Unread;

/// <inheritdoc />
internal class FirstUnreadService : IFirstUnreadService
{
    private readonly IFirstUnreadRepository _firstUnreadRepository;
    private readonly IGameService _gameService;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;

    public FirstUnreadService(
        IFirstUnreadRepository firstUnreadRepository,
        IGameService gameService,
        IUnreadCountersRepository unreadCountersRepository,
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager)
    {
        _firstUnreadRepository = firstUnreadRepository;
        _gameService = gameService;
        _unreadCountersRepository = unreadCountersRepository;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
    }

    /// <inheritdoc />
    public async Task<FirstUnreadPostResult> GetFirstUnreadPost(Guid gameId)
    {
        var game = await _gameService.GetAsync(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Read, game);

        var userId = _identityProvider.Current.User.UserId;
        var isAuthenticated = _identityProvider.Current.User.IsAuthenticated;

        var roomIds = await _firstUnreadRepository.GetAccessibleRoomIds(gameId, userId);

        if (roomIds.Count == 0)
        {
            return new FirstUnreadPostResult { HasUnread = false };
        }

        // For anonymous users - return first post of the first room
        if (!isAuthenticated)
        {
            return await _firstUnreadRepository.GetFirstPostInRooms(roomIds)
                ?? new FirstUnreadPostResult { HasUnread = false };
        }

        // Get last read times for all rooms
        var roomIdsArray = roomIds is Guid[] arr ? arr : roomIds.ToArray();
        var lastReadTimes = await _unreadCountersRepository.GetLastReadTimes(
            userId, UnreadEntryType.Message, roomIdsArray);

        // Find first unread post
        var result = await _firstUnreadRepository.FindFirstUnreadPost(roomIds, lastReadTimes);
        if (result != null)
        {
            return result;
        }

        // No unread posts - return last post of the last room
        return await _firstUnreadRepository.GetLastPostInRooms(roomIds)
            ?? new FirstUnreadPostResult { HasUnread = false };
    }

    public async Task<FirstUnreadCommentResult> GetFirstUnreadComment(Guid gameId)
    {
        var game = await _gameService.GetAsync(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.ReadComments, game);

        var userId = _identityProvider.Current.User.UserId;
        var isAuthenticated = _identityProvider.Current.User.IsAuthenticated;

        // For anonymous users - return first comment
        if (!isAuthenticated)
        {
            return await _firstUnreadRepository.GetFirstComment(gameId)
                ?? new FirstUnreadCommentResult { HasUnread = false };
        }

        // Get last read time for comments
        var lastRead = await _unreadCountersRepository.GetLastReadTime(
            userId, gameId, UnreadEntryType.Message) ?? DateTime.MinValue;

        // Find first unread comment
        var result = await _firstUnreadRepository.FindFirstUnreadComment(gameId, lastRead);
        if (result != null)
        {
            return result;
        }

        // No unread comments - return last comment
        return await _firstUnreadRepository.GetLastComment(gameId)
            ?? new FirstUnreadCommentResult { HasUnread = false };
    }
}
