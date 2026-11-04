using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;

namespace DM.Domain.Forum.Features.Boards;

/// <inheritdoc />
internal class BoardService : IBoardService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IBoardRepository _boardRepository;
    private readonly IBoardModeratorRepository _moderatorRepository;
    private readonly IUserReadRepository _userRepository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly ICache _cache;

    public BoardService(
        IIdentityProvider identityProvider,
        IBoardRepository boardRepository,
        IBoardModeratorRepository moderatorRepository,
        IUserReadRepository userRepository,
        IUnreadCountersRepository unreadCountersRepository,
        ICache cache)
    {
        _identityProvider = identityProvider;
        _boardRepository = boardRepository;
        _moderatorRepository = moderatorRepository;
        _userRepository = userRepository;
        _unreadCountersRepository = unreadCountersRepository;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Board>> GetBoardsList()
    {
        var boards = await GetBoards();
        var identity = _identityProvider.Current;

        // A guest cannot mark anything read, so a guest is shown the totals
        if (!identity.User.IsAuthenticated)
        {
            foreach (var board in boards)
            {
                board.UnreadTopicsCount = board.TopicsCount;
                board.UnreadCommentsCount = board.CommentsCount;
            }
            return boards;
        }

        // Sequential on purpose: both fills query the one DmDbContext of this
        // scope, and a context refuses parallel operations.
        await _unreadCountersRepository.FillParentCounters(boards, identity.User.UserId,
            b => b.Id, b => b.UnreadTopicsCount);
        await _unreadCountersRepository.FillTotalUnreadCounters(boards, identity.User.UserId,
            b => b.Id, b => b.UnreadCommentsCount);

        return boards;
    }

    /// <inheritdoc />
    public async Task<Board> GetBoardWithCounters(string aliasOrTitle)
    {
        var board = await GetBoard(aliasOrTitle);
        var identity = _identityProvider.Current;
        if (identity.User.IsAuthenticated)
        {
            // Sequential on purpose: both selects query the one DmDbContext of
            // this scope, and a context refuses parallel operations.
            var topics = await _unreadCountersRepository.SelectByParentsAsync(
                identity.User.UserId, UnreadEntryType.Message, board.Id);
            var comments = await _unreadCountersRepository.SelectTotalUnreadByParentsAsync(
                identity.User.UserId, UnreadEntryType.Message, board.Id);

            board.UnreadTopicsCount = topics[board.Id];
            board.UnreadCommentsCount = comments[board.Id];
        }
        else
        {
            board.UnreadTopicsCount = board.TopicsCount;
            board.UnreadCommentsCount = board.CommentsCount;
        }

        return board;
    }

    /// <inheritdoc />
    public async Task<Board> GetBoard(string aliasOrTitle)
    {
        var boards = await GetBoards();

        // A board GUID too, for the endpoints that pass the board id (GET boards/{id}/moderators).
        var byId = Guid.TryParse(aliasOrTitle, out var boardId);

        var board = boards.FirstOrDefault(b =>
            (byId && b.Id == boardId) ||
            string.Equals(b.Alias, aliasOrTitle, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(b.Title, aliasOrTitle, StringComparison.OrdinalIgnoreCase));
        if (board == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BoardNotFound(aliasOrTitle));
        }

        return board;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetModerators(string aliasOrTitle)
    {
        var board = await GetBoard(aliasOrTitle);
        return await _cache.GetOrCreateAsync(
            ModeratorsCacheKey(board.Id),
            () => _moderatorRepository.Get(board.Id),
            CachePolicy.LongLived);
    }

    /// <inheritdoc />
    public async Task<GeneralUser> AddModerator(string aliasOrTitle, string username)
    {
        var board = await GetBoard(aliasOrTitle);
        var user = await _userRepository.GetUserAsync(username);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(username));
        }

        var isAlreadyModerator = await _moderatorRepository.IsModerator(board.Id, user.UserId);
        if (isAlreadyModerator)
        {
            throw new HttpException(HttpStatusCode.Conflict, $"Пользователь {username} уже модератор этого раздела");
        }

        await _moderatorRepository.Add(board.Id, user.UserId);
        await _cache.InvalidateAsync(ModeratorsCacheKey(board.Id));
        return user;
    }

    /// <inheritdoc />
    public async Task RemoveModerator(string aliasOrTitle, string username)
    {
        var board = await GetBoard(aliasOrTitle);
        // Only the identifier leaves this method, and GetUserAsync pays for the whole
        // achievement profile to hand it over.
        var userId = await _userRepository.FindUserIdAsync(username);
        if (userId == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.UserNotFoundByUsername(username));
        }

        var isModerator = await _moderatorRepository.IsModerator(board.Id, userId.Value);
        if (!isModerator)
        {
            throw new HttpException(HttpStatusCode.NotFound, $"Пользователь {username} не модератор этого раздела");
        }

        await _moderatorRepository.Remove(board.Id, userId.Value);
        await _cache.InvalidateAsync(ModeratorsCacheKey(board.Id));
    }

    private static string ModeratorsCacheKey(Guid boardId) => $"board_moderators_{boardId}";

    /// <inheritdoc />
    public async Task<IEnumerable<Board>> GetBoardsWithoutCounters() => await GetBoards();

    private async Task<Board[]> GetBoards() => (await _boardRepository.SelectBoards()).ToArray();

    /// <inheritdoc />
    public async Task<Board> GetBoardByAlias(string alias)
    {
        var boards = await GetBoards();
        var board = boards.FirstOrDefault(b =>
            string.Equals(b.Alias, alias, StringComparison.OrdinalIgnoreCase));
        if (board == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BoardNotFound(alias));
        }

        return board;
    }
}
