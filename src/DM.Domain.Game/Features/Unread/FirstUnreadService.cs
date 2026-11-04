using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Game.Authorization;

namespace DM.Domain.Game.Features.Unread;

/// <inheritdoc />
internal class FirstUnreadService : IFirstUnreadService
{
    private readonly IFirstUnreadRepository _firstUnreadRepository;
    private readonly IGameService _gameService;
    private readonly IRoomService _roomService;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;

    public FirstUnreadService(
        IFirstUnreadRepository firstUnreadRepository,
        IGameService gameService,
        IRoomService roomService,
        IUnreadCountersRepository unreadCountersRepository,
        IIdentityProvider identityProvider,
        IIntentionManager intentionManager)
    {
        _firstUnreadRepository = firstUnreadRepository;
        _gameService = gameService;
        _roomService = roomService;
        _unreadCountersRepository = unreadCountersRepository;
        _identityProvider = identityProvider;
        _intentionManager = intentionManager;
    }

    /// <inheritdoc />
    public async Task<FirstUnreadPostResult> GetFirstUnreadPost(Guid gameId)
    {
        var game = await _gameService.GetAsync(gameId);
        _intentionManager.ThrowIfForbidden(GameIntention.Read, game);

        var user = _identityProvider.Current.User;
        var roomIds = await _firstUnreadRepository.GetAccessibleRoomIds(gameId, user.UserId);

        if (roomIds.Count == 0)
        {
            return new FirstUnreadPostResult { HasUnread = false };
        }

        // A guest is put on the first post of the first room.
        if (!user.IsAuthenticated)
        {
            return await _firstUnreadRepository.GetFirstPostInRooms(roomIds)
                ?? new FirstUnreadPostResult { HasUnread = false };
        }

        var lastReadTimes = await _unreadCountersRepository.GetLastReadTimesAsync(
            user.UserId, UnreadEntryType.Message, roomIds.ToArray());

        var result = await _firstUnreadRepository.FindFirstUnreadPost(roomIds, lastReadTimes);
        if (result != null)
        {
            return result;
        }

        return await _firstUnreadRepository.GetLastPostInRooms(roomIds)
            ?? new FirstUnreadPostResult { HasUnread = false };
    }

    /// <inheritdoc />
    public async Task<FirstUnreadRoomPostResult> GetFirstUnreadPostInRoom(Guid roomId, int? pageSize)
    {
        // The gate of reading the room's posts.
        await _roomService.GetAsync(roomId);

        var identity = _identityProvider.Current;
        var size = pageSize ?? identity.Settings.PageSize;

        // Nothing is marked read for a guest, so the room opens on its first post.
        var lastRead = DateTime.MinValue;
        if (identity.User.IsAuthenticated)
        {
            var marked = await _unreadCountersRepository.GetLastReadTimeAsync(
                identity.User.UserId, roomId, UnreadEntryType.Message) ?? DateTime.MinValue;

            // The reader's own post is not news: the mark is lifted to it, so the unread posts stay
            // the tail of the room, which LocateInRoom counts on.
            var ownLast = await _firstUnreadRepository.GetLastOwnPostUtc(roomId, identity.User.UserId);
            lastRead = ownLast > marked ? ownLast.Value : marked;
        }

        var result = await _firstUnreadRepository.LocateInRoom(roomId, lastRead);
        result.PageSize = size;
        result.Page = PagingResult.Create(0, result.PostNumber, size).CurrentPage;
        return result;
    }

    public async Task<FirstUnreadCommentResult> GetFirstUnreadComment(Guid gameId)
    {
        await _gameService.GetAsync(gameId);

        var user = _identityProvider.Current.User;
        if (!user.IsAuthenticated)
        {
            return await _firstUnreadRepository.GetFirstComment(gameId)
                ?? new FirstUnreadCommentResult { HasUnread = false };
        }

        var lastRead = await _unreadCountersRepository.GetLastReadTimeAsync(
            user.UserId, gameId, UnreadEntryType.Message) ?? DateTime.MinValue;

        var result = await _firstUnreadRepository.FindFirstUnreadComment(gameId, lastRead);
        if (result != null)
        {
            return result;
        }

        return await _firstUnreadRepository.GetLastComment(gameId)
            ?? new FirstUnreadCommentResult { HasUnread = false };
    }
}
