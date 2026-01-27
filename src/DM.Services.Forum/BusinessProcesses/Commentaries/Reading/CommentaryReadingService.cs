using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.Forum.BusinessProcesses.Boards;
using DM.Services.Forum.BusinessProcesses.Topics.Reading;
using Comment = DM.Services.Common.Dto.Comment;

namespace DM.Services.Forum.BusinessProcesses.Commentaries.Reading;

/// <inheritdoc />
internal class CommentaryReadingService : ICommentaryReadingService
{
    private readonly ITopicReadingService _topicReadingService;
    private readonly IBoardReadingService _boardReadingService;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly ICommentaryReadingRepository _commentaryRepository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public CommentaryReadingService(
        ITopicReadingService topicReadingService,
        IBoardReadingService boardReadingService,
        IIdentityProvider identityProvider,
        IUnreadCountersRepository unreadCountersRepository,
        ICommentaryReadingRepository commentaryRepository)
    {
        _topicReadingService = topicReadingService;
        _boardReadingService = boardReadingService;
        _unreadCountersRepository = unreadCountersRepository;
        _commentaryRepository = commentaryRepository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Comment> comments, PagingResult paging)> Get(
        Guid topicId, PagingQuery query)
    {
        await _topicReadingService.GetTopic(topicId);

        var totalCount = await _commentaryRepository.Count(topicId);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.CommentsPerPage, totalCount);

        var comments = await _commentaryRepository.Get(topicId, paging);

        return (comments, paging.Result);
    }

    /// <inheritdoc />
    public async Task<Comment> Get(Guid commentId)
    {
        return await _commentaryRepository.Get(commentId) ??
               throw new HttpException(HttpStatusCode.Gone, $"Comment {commentId} not found");
    }

    /// <inheritdoc />
    public async Task MarkAsRead(Guid topicId)
    {
        await _topicReadingService.GetTopic(topicId);
        await _unreadCountersRepository.Flush(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, topicId);
    }

    /// <inheritdoc />
    public async Task MarkAsRead(string boardTitle)
    {
        var board = await _boardReadingService.GetBoard(boardTitle);
        await _unreadCountersRepository.FlushAll(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, board.Id);
    }

    /// <inheritdoc />
    public async Task MarkAllAsRead()
    {
        var boards = await _boardReadingService.GetBoardsList();
        var userId = _identityProvider.Current.User.UserId;
        foreach (var board in boards)
        {
            await _unreadCountersRepository.FlushAll(userId, UnreadEntryType.Message, board.Id);
        }
    }
}