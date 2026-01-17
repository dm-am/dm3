using System;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.BusinessObjects.Boards;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Forum.Authorization;
using DM.Services.Forum.BusinessProcesses.Topics.Reading;
using DM.Services.Forum.BusinessProcesses.Topics.Updating;
using DM.Services.MessageQueuing.GeneralBus;

namespace DM.Services.Forum.BusinessProcesses.Topics.Deleting;

/// <inheritdoc />
internal class TopicDeletingService : ITopicDeletingService
{
    private readonly ITopicReadingService _topicReadingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly ITopicUpdatingRepository _repository;
    private readonly IInvokedEventProducer _invokedEventProducer;
    private readonly IUnreadCountersRepository _unreadCountersRepository;

    /// <inheritdoc />
    public TopicDeletingService(
        ITopicReadingService topicReadingService,
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        ITopicUpdatingRepository repository,
        IInvokedEventProducer invokedEventProducer,
        IUnreadCountersRepository unreadCountersRepository)
    {
        _topicReadingService = topicReadingService;
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _invokedEventProducer = invokedEventProducer;
        _unreadCountersRepository = unreadCountersRepository;
    }

    /// <inheritdoc />
    public async Task DeleteTopic(Guid topicId)
    {
        var topic = await _topicReadingService.GetTopic(topicId);
        _intentionManager.ThrowIfForbidden(ForumIntention.AdministrateTopics, topic.Forum);

        await _repository.Update(_updateBuilderFactory.Create<ForumTopic>(topicId).Field(t => t.IsRemoved, true));
        await _unreadCountersRepository.Delete(topicId, UnreadEntryType.Message);
        await _invokedEventProducer.Send(EventType.DeletedForumTopic, topicId);
    }
}