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
    private readonly IForumReadingService _forumReadingService;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly ICommentaryReadingRepository _commentaryRepository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public CommentaryReadingService(
        ITopicReadingService topicReadingService,
        IForumReadingService forumReadingService,
        IIdentityProvider identityProvider,
        IUnreadCountersRepository unreadCountersRepository,
        ICommentaryReadingRepository commentaryRepository)
    {
        _topicReadingService = topicReadingService;
        _forumReadingService = forumReadingService;
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
    public async Task MarkAsRead(string forumTitle)
    {
        var forum = await _forumReadingService.GetForum(forumTitle);
        await _unreadCountersRepository.FlushAll(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, forum.Id);
    }

    /// <inheritdoc />
    public async Task MarkAllAsRead()
    {
        var forums = await _forumReadingService.GetForaList();
        var userId = _identityProvider.Current.User.UserId;
        foreach (var forum in forums)
        {
            await _unreadCountersRepository.FlushAll(userId, UnreadEntryType.Message, forum.Id);
        }
    }
}