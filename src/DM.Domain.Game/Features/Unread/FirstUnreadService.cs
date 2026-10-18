using System;
using System.Linq;
using System.Threading.Tasks;
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
        var lastReadTimes = await _unreadCountersRepository.GetLastReadTimesAsync(
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

    /// <inheritdoc />
    public async Task<FirstUnreadRoomPostResult> GetFirstUnreadPostInRoom(Guid roomId, int? pageSize)
    {
        // The same gate as reading the room's posts: a room outside the reader's
        // scope is absent, and GetAsync answers 404 for it.
        await _roomService.GetAsync(roomId);

        var identity = _identityProvider.Current;
        var size = pageSize ?? identity.Settings.Paging.PostsPerPage;

        // Nothing is marked read for a reader who is not signed in, so everything is
        // unread and the room opens on its first post, as the game does.
        var lastRead = DateTime.MinValue;
        if (identity.User.IsAuthenticated)
        {
            var marked = await _unreadCountersRepository.GetLastReadTimeAsync(
                identity.User.UserId, roomId, UnreadEntryType.Message) ?? DateTime.MinValue;

            // The reader's own post is not news to them. The room is marked read when
            // it is opened, not when a post is sent from it, so the mark alone would
            // open the next visit on the reader's own post. The mark is lifted to that
            // post rather than the reader's posts being skipped: the unread ones stay
            // the tail of the room, which LocateInRoom counts on, and the posts of
            // others written before it are read, since the reader wrote after them.
            var ownLast = await _firstUnreadRepository.GetLastOwnPostUtc(roomId, identity.User.UserId);
            lastRead = ownLast > marked ? ownLast.Value : marked;
        }

        var result = await _firstUnreadRepository.LocateInRoom(roomId, lastRead);
        result.PageSize = size;
        result.Page = Math.Max(1, (result.PostNumber + size - 1) / size);
        return result;
    }

    public async Task<FirstUnreadCommentResult> GetFirstUnreadComment(Guid gameId)
    {
        // The discussion is read by whoever may read the game: the read below.
        await _gameService.GetAsync(gameId);

        var userId = _identityProvider.Current.User.UserId;
        var isAuthenticated = _identityProvider.Current.User.IsAuthenticated;

        // For anonymous users - return first comment
        if (!isAuthenticated)
        {
            return await _firstUnreadRepository.GetFirstComment(gameId)
                ?? new FirstUnreadCommentResult { HasUnread = false };
        }

        // Get last read time for comments
        var lastRead = await _unreadCountersRepository.GetLastReadTimeAsync(
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
