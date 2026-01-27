using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.BusinessObjects.Boards;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Forum.Authorization;
using DM.Services.Forum.BusinessProcesses.Boards;
using DM.Services.Forum.BusinessProcesses.Topics.Reading;
using DM.Services.Forum.Dto.Input;
using DM.Services.Forum.Dto.Output;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;

namespace DM.Services.Forum.BusinessProcesses.Topics.Updating;

/// <inheritdoc />
internal class TopicUpdatingService : ITopicUpdatingService
{
    private readonly IValidator<UpdateTopic> _validator;
    private readonly ITopicReadingService _topicReadingService;
    private readonly IBoardReadingService _boardReadingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly ITopicUpdatingRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IInvokedEventProducer _invokedEventProducer;

    /// <inheritdoc />
    public TopicUpdatingService(
        IValidator<UpdateTopic> validator,
        ITopicReadingService topicReadingService,
        IBoardReadingService boardReadingService,
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        ITopicUpdatingRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IInvokedEventProducer invokedEventProducer)
    {
        _validator = validator;
        _topicReadingService = topicReadingService;
        _boardReadingService = boardReadingService;
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _invokedEventProducer = invokedEventProducer;
    }

    /// <inheritdoc />
    public async Task<Topic> UpdateTopic(UpdateTopic updateTopic)
    {
        await _validator.ValidateAndThrowAsync(updateTopic);
        var oldTopic = await _topicReadingService.GetTopic(updateTopic.TopicId);

        _intentionManager.ThrowIfForbidden(TopicIntention.Edit, oldTopic);

        var changes = _updateBuilderFactory.Create<ForumTopic>(updateTopic.TopicId)
            .MaybeField(t => t.Title, updateTopic.Title?.Trim())
            .MaybeField(t => t.Text, updateTopic.Text?.Trim());

        if (_intentionManager.IsAllowed(ForumIntention.AdministrateTopics, oldTopic.Board))
        {
            changes
                .MaybeField(t => t.IsClosed, updateTopic.IsClosed)
                .MaybeField(t => t.IsAttached, updateTopic.IsAttached);

            if (updateTopic.BoardTitle != default &&
                oldTopic.Board.Title != updateTopic.BoardTitle)
            {
                var board = await _boardReadingService.GetBoard(updateTopic.BoardTitle, false);
                _intentionManager.ThrowIfForbidden(ForumIntention.CreateTopic, board);
                changes.Field(t => t.BoardId, board.Id);
                await _unreadCountersRepository.ChangeParent(oldTopic.Board.Id, UnreadEntryType.Message, board.Id);
            }
        }

        var topic = await _repository.Update(changes);
        await _invokedEventProducer.Send(EventType.ChangedForumTopic, topic.Id);

        return topic;
    }
}