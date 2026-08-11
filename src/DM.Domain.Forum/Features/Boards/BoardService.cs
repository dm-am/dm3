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
    private readonly IAccessPolicyConverter _accessPolicyConverter;
    private readonly IBoardRepository _boardRepository;
    private readonly IBoardModeratorRepository _moderatorRepository;
    private readonly IUserReadRepository _userRepository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly ICache _cache;

    /// <inheritdoc />
    public BoardService(
        IIdentityProvider identityProvider,
        IAccessPolicyConverter accessPolicyConverter,
        IBoardRepository boardRepository,
        IBoardModeratorRepository moderatorRepository,
        IUserReadRepository userRepository,
        IUnreadCountersRepository unreadCountersRepository,
        ICache cache)
    {
        _identityProvider = identityProvider;
        _accessPolicyConverter = accessPolicyConverter;
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

        // Anonymous users: show total counts (they can't mark anything as read)
        if (!identity.User.IsAuthenticated)
        {
            foreach (var board in boards)
            {
                board.UnreadTopicsCount = board.TopicsCount;
                board.UnreadCommentsCount = board.CommentsCount;
            }
            return boards;
        }

        var boardsCopy = boards.Select(b => new Board
        {
            Id = b.Id,
            Title = b.Title,
            Alias = b.Alias,
            Description = b.Description,
            CreateTopicPolicy = b.CreateTopicPolicy,
            ViewPolicy = b.ViewPolicy,
            ModeratorIds = b.ModeratorIds,
            TopicsCount = b.TopicsCount,
            CommentsCount = b.CommentsCount,
            UnreadTopicsCount = 0,
            UnreadCommentsCount = 0,
            LastComment = b.LastComment
        }).ToArray();

        var fillTopicsTask = _unreadCountersRepository.FillParentCounters(boardsCopy, identity.User.UserId,
            b => b.Id, b => b.UnreadTopicsCount);
        var fillCommentsTask = _unreadCountersRepository.FillTotalUnreadCounters(boardsCopy, identity.User.UserId,
            b => b.Id, b => b.UnreadCommentsCount);
        await Task.WhenAll(fillTopicsTask, fillCommentsTask);

        return boardsCopy;
    }

    /// <inheritdoc />
    public async Task<Board> GetSingleBoard(string boardTitle)
    {
        var board = await GetBoard(boardTitle);
        var identity = _identityProvider.Current;
        if (identity.User.IsAuthenticated)
        {
            var topicsTask = _unreadCountersRepository.SelectByParentsAsync(
                identity.User.UserId, UnreadEntryType.Message, board.Id);
            var commentsTask = _unreadCountersRepository.SelectTotalUnreadByParentsAsync(
                identity.User.UserId, UnreadEntryType.Message, board.Id);
            var topics = await topicsTask;
            var comments = await commentsTask;

            board.UnreadTopicsCount = topics[board.Id];
            board.UnreadCommentsCount = comments[board.Id];
        }
        else
        {
            // Anonymous users: show total counts
            board.UnreadTopicsCount = board.TopicsCount;
            board.UnreadCommentsCount = board.CommentsCount;
        }

        return board;
    }

    /// <inheritdoc />
    public async Task<Board> GetBoard(string aliasOrTitle, bool onlyAvailable = true)
    {
        var boards = await GetBoards(onlyAvailable);

        // Accept a board GUID as well as alias/title so endpoints that pass
        // the board id (e.g. GET boards/{id}/moderators) resolve correctly.
        var byId = System.Guid.TryParse(aliasOrTitle, out var boardId);

        var board = boards.FirstOrDefault(b =>
            (byId && b.Id == boardId) ||
            string.Equals(b.Alias, aliasOrTitle, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(b.Title, aliasOrTitle, System.StringComparison.OrdinalIgnoreCase));
        if (board == null)
        {
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.BoardNotFound(aliasOrTitle));
        }

        return board;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetModerators(string boardTitle)
    {
        var board = await GetBoard(boardTitle);
        return await _cache.GetOrCreateAsync(
            $"board_moderators_{board.Id}",
            () => _moderatorRepository.Get(board.Id),
            CachePolicy.LongLived);
    }

    /// <inheritdoc />
    public async Task<GeneralUser> AddModerator(string boardTitle, string username)
    {
        var board = await GetBoard(boardTitle, onlyAvailable: false);
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
        await _cache.InvalidateAsync($"board_moderators_{board.Id}");
        return user;
    }

    /// <inheritdoc />
    public async Task RemoveModerator(string boardTitle, string username)
    {
        var board = await GetBoard(boardTitle, onlyAvailable: false);
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
        await _cache.InvalidateAsync($"board_moderators_{board.Id}");
    }

    private async Task<Board[]> GetBoards(bool onlyAvailable = true)
    {
        var accessPolicy = onlyAvailable
            ? _accessPolicyConverter.Convert(_identityProvider.Current.User.Role)
            : (BoardAccessPolicy?)null;
        return (await _boardRepository.SelectBoards(accessPolicy)).ToArray();
    }

    /// <inheritdoc />
    public async Task<Board> GetBoardByAlias(string alias)
    {
        var boards = await GetBoards();
        var board = boards.FirstOrDefault(b =>
            string.Equals(b.Alias, alias, System.StringComparison.OrdinalIgnoreCase));
        if (board == null)
        {
            throw new HttpException(HttpStatusCode.Gone, RefusalMessage.BoardNotFound(alias));
        }

        return board;
    }
}
