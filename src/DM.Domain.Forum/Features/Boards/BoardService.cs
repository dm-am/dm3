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

namespace DM.Domain.Forum.Features.Boards;

/// <inheritdoc />
internal class BoardService : IBoardService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IAccessPolicyConverter _accessPolicyConverter;
    private readonly IBoardRepository _boardRepository;
    private readonly IBoardModeratorRepository _moderatorRepository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly ICache _cache;

    /// <inheritdoc />
    public BoardService(
        IIdentityProvider identityProvider,
        IAccessPolicyConverter accessPolicyConverter,
        IBoardRepository boardRepository,
        IBoardModeratorRepository moderatorRepository,
        IUnreadCountersRepository unreadCountersRepository,
        ICache cache)
    {
        _identityProvider = identityProvider;
        _accessPolicyConverter = accessPolicyConverter;
        _boardRepository = boardRepository;
        _moderatorRepository = moderatorRepository;
        _unreadCountersRepository = unreadCountersRepository;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Board>> GetBoardsList()
    {
        var boards = await GetBoards();
        var identity = _identityProvider.Current;

        if (!identity.User.IsAuthenticated)
        {
            return boards;
        }

        var boardsCopy = boards.Select(b => new Board
        {
            Id = b.Id,
            Title = b.Title,
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

        return board;
    }

    /// <inheritdoc />
    public async Task<Board> GetBoard(string boardTitle, bool onlyAvailable = true)
    {
        var board = (await GetBoards(onlyAvailable)).FirstOrDefault(b => b.Title == boardTitle);
        if (board == null)
        {
            throw new HttpException(HttpStatusCode.Gone, $"Board {boardTitle} not found");
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

    private async Task<Board[]> GetBoards(bool onlyAvailable = true)
    {
        var accessPolicy = onlyAvailable
            ? _accessPolicyConverter.Convert(_identityProvider.Current.User.Role)
            : (BoardAccessPolicy?)null;
        return (await _boardRepository.SelectBoards(accessPolicy)).ToArray();
    }
}
