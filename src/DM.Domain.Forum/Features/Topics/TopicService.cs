using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Forum.Authorization;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Core.Events;
using FluentValidation;

namespace DM.Domain.Forum.Features.Topics;

/// <inheritdoc />
internal class TopicService : ITopicService
{
    private readonly IValidator<CreateTopic> _createValidator;
    private readonly IValidator<UpdateTopic> _updateValidator;
    private readonly IBoardService _boardService;
    private readonly IAccessPolicyConverter _accessPolicyConverter;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly ITopicRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IEventProducer _invokedEventProducer;

    public TopicService(
        IValidator<CreateTopic> createValidator,
        IValidator<UpdateTopic> updateValidator,
        IBoardService boardService,
        IAccessPolicyConverter accessPolicyConverter,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        ITopicRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IEventProducer invokedEventProducer)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _boardService = boardService;
        _accessPolicyConverter = accessPolicyConverter;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _invokedEventProducer = invokedEventProducer;
    }

    /// <inheritdoc />
    public async Task<Topic> CreateAsync(CreateTopic createTopic, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(createTopic, ct);

        var board = await _boardService.GetBoard(createTopic.BoardTitle);
        _intentionManager.ThrowIfForbidden(ForumIntention.CreateTopic, board);

        var createEntity = new CreateTopicEntity
        {
            Title = createTopic.Title,
            Text = createTopic.Text
        };
        var topic = await _repository.Create(
            createEntity,
            _identityProvider.Current.User.UserId,
            board.Id,
            ct);

        await Task.WhenAll(
            _invokedEventProducer.SendAsync(EventType.NewTopic, topic.Id),
            _unreadCountersRepository.CreateAsync(topic.Id, board.Id, UnreadEntryType.Message));

        return topic;
    }

    /// <inheritdoc />
    public async Task<Topic> GetAsync(Guid topicId, CancellationToken ct = default)
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
            topic.UnreadCommentsCount = (await _unreadCountersRepository.SelectByEntitiesAsync(
                identity.User.UserId, UnreadEntryType.Message, topicId))[topicId];
        }

        return topic;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Topic> topics, PagingResult paging)> GetListAsync(
        string boardTitle, PagingQuery query, CancellationToken ct = default)
    {
        var board = await _boardService.GetBoard(boardTitle);

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
    public async Task<IEnumerable<Topic>> GetAttachedAsync(string boardTitle, CancellationToken ct = default)
    {
        var board = await _boardService.GetBoard(boardTitle);
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
    public async Task<Topic> UpdateAsync(UpdateTopic updateTopic, CancellationToken ct = default)
    {
        await _updateValidator.ValidateAndThrowAsync(updateTopic, ct);
        var oldTopic = await GetAsync(updateTopic.TopicId, ct);

        _intentionManager.ThrowIfForbidden(TopicIntention.Edit, oldTopic);

        Guid? newBoardId = null;

        if (_intentionManager.IsAllowed(ForumIntention.AdministrateTopics, oldTopic.Board))
        {
            if (updateTopic.BoardTitle != default &&
                oldTopic.Board.Title != updateTopic.BoardTitle)
            {
                var board = await _boardService.GetBoard(updateTopic.BoardTitle, false);
                _intentionManager.ThrowIfForbidden(ForumIntention.CreateTopic, board);
                newBoardId = board.Id;
                await _unreadCountersRepository.ChangeParentAsync(oldTopic.Board.Id, UnreadEntryType.Message, board.Id);
            }
        }
        else
        {
            // Non-admin users cannot change these fields
            updateTopic.IsClosed = null;
            updateTopic.IsAttached = null;
        }

        var updateEntity = new UpdateTopicEntity
        {
            TopicId = updateTopic.TopicId,
            Title = updateTopic.Title,
            Text = updateTopic.Text,
            IsClosed = updateTopic.IsClosed,
            IsAttached = updateTopic.IsAttached
        };
        var topic = await _repository.Update(updateEntity, newBoardId);
        await _invokedEventProducer.SendAsync(EventType.ChangedTopic, topic.Id);

        return topic;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid topicId, CancellationToken ct = default)
    {
        var topic = await GetAsync(topicId, ct);
        _intentionManager.ThrowIfForbidden(ForumIntention.AdministrateTopics, topic.Board);

        await _repository.Delete(topicId);
        await _unreadCountersRepository.DeleteAsync(topicId, UnreadEntryType.Message);
        await _invokedEventProducer.SendAsync(EventType.DeletedTopic, topicId);
    }
}
