using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Common.Extensions;
using DM.Services.Core.Dto;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.Forum.BusinessProcesses.Common;
using DM.Services.Forum.BusinessProcesses.Boards;
using DM.Services.Forum.Dto.Output;

namespace DM.Services.Forum.BusinessProcesses.Topics.Reading;

/// <inheritdoc />
internal class TopicReadingService : ITopicReadingService
{
    private readonly IBoardReadingService _boardReadingService;
    private readonly IAccessPolicyConverter _accessPolicyConverter;
    private readonly ITopicReadingRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public TopicReadingService(
        IIdentityProvider identityProvider,
        IBoardReadingService boardReadingService,
        IAccessPolicyConverter accessPolicyConverter,
        ITopicReadingRepository repository,
        IUnreadCountersRepository unreadCountersRepository)
    {
        _identityProvider = identityProvider;
        _boardReadingService = boardReadingService;
        _accessPolicyConverter = accessPolicyConverter;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Topic> topics, PagingResult paging)> GetTopicsList(
        string boardTitle, PagingQuery query, CancellationToken ct = default)
    {
        var board = await _boardReadingService.GetBoard(boardTitle);

        var totalCount = await _repository.Count(board.Id, ct);
        var identity = _identityProvider.Current;
        var pagingData = new PagingData(query, identity.Settings.Paging.TopicsPerPage, totalCount);

        var topics = (await _repository.Get(board.Id, pagingData, false, ct)).ToArray();
        if (identity.User.IsAuthenticated)
        {
            await _unreadCountersRepository.FillEntityCounters(topics, identity.User.UserId,
                t => t.Id, t => t.UnreadCommentsCount);
        }

        return (topics, pagingData.Result);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Topic>> GetAttachedTopics(string boardTitle, CancellationToken ct = default)
    {
        var board = await _boardReadingService.GetBoard(boardTitle);
        var topics = (await _repository.Get(board.Id, null, true, ct)).ToArray();
        var identity = _identityProvider.Current;
        if (identity.User.IsAuthenticated)
        {
            await _unreadCountersRepository.FillEntityCounters(topics, identity.User.UserId,
                t => t.Id, t => t.UnreadCommentsCount);
        }

        return topics;
    }

    /// <inheritdoc />
    public async Task<Topic> GetTopic(Guid topicId, CancellationToken ct = default)
    {
        var identity = _identityProvider.Current;
        var accessPolicy = _accessPolicyConverter.Convert(identity.User.Role);
        var topic = await _repository.Get(topicId, accessPolicy, ct);
        if (topic == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Topic not found");
        }

        if (identity.User.IsAuthenticated)
        {
            topic.UnreadCommentsCount = (await _unreadCountersRepository.SelectByEntities(
                identity.User.UserId, UnreadEntryType.Message, topicId))[topicId];
        }

        return topic;
    }
}