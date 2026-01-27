using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Common.Extensions;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.Forum.BusinessProcesses.Common;

namespace DM.Services.Forum.BusinessProcesses.Boards;

/// <inheritdoc />
internal class BoardReadingService : IBoardReadingService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IAccessPolicyConverter _accessPolicyConverter;
    private readonly IBoardRepository _boardRepository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;

    /// <inheritdoc />
    public BoardReadingService(
        IIdentityProvider identityProvider,
        IAccessPolicyConverter accessPolicyConverter,
        IBoardRepository boardRepository,
        IUnreadCountersRepository unreadCountersRepository)
    {
        _identityProvider = identityProvider;
        _accessPolicyConverter = accessPolicyConverter;
        _boardRepository = boardRepository;
        _unreadCountersRepository = unreadCountersRepository;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Dto.Output.Board>> GetBoardsList()
    {
        var boards = await GetBoards();
        var identity = _identityProvider.Current;

        // Для анонимных пользователей возвращаем объекты из кэша напрямую
        if (!identity.User.IsAuthenticated)
        {
            return boards;
        }

        // Для авторизованных — копируем, чтобы не загрязнять кэш персональными данными
        var boardsCopy = boards.Select(b => new Dto.Output.Board
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
    public async Task<Dto.Output.Board> GetSingleBoard(string boardTitle)
    {
        var board = await GetBoard(boardTitle);
        var identity = _identityProvider.Current;
        if (identity.User.IsAuthenticated)
        {
            var topicsTask = _unreadCountersRepository.SelectByParents(
                identity.User.UserId, UnreadEntryType.Message, board.Id);
            var commentsTask = _unreadCountersRepository.SelectTotalUnreadByParents(
                identity.User.UserId, UnreadEntryType.Message, board.Id);
            var topics = await topicsTask;
            var comments = await commentsTask;

            board.UnreadTopicsCount = topics[board.Id];
            board.UnreadCommentsCount = comments[board.Id];
        }

        return board;
    }

    /// <inheritdoc />
    public async Task<Dto.Output.Board> GetBoard(string boardTitle, bool onlyAvailable = true)
    {
        var board = (await GetBoards(onlyAvailable)).FirstOrDefault(b => b.Title == boardTitle);
        if (board == null)
        {
            throw new HttpException(HttpStatusCode.Gone, $"Board {boardTitle} not found");
        }

        return board;
    }

    private async Task<Dto.Output.Board[]> GetBoards(bool onlyAvailable = true)
    {
        var accessPolicy = onlyAvailable
            ? _accessPolicyConverter.Convert(_identityProvider.Current.User.Role)
            : (BoardAccessPolicy?) null;
        return (await _boardRepository.SelectBoards(accessPolicy)).ToArray();
    }
}