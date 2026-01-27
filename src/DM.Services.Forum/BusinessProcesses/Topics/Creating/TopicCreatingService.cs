using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Tracing;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.Forum.Authorization;
using DM.Services.Forum.BusinessProcesses.Boards;
using DM.Services.Forum.Dto.Input;
using DM.Services.Forum.Dto.Output;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;

namespace DM.Services.Forum.BusinessProcesses.Topics.Creating;

/// <inheritdoc />
internal class TopicCreatingService : ITopicCreatingService
{
    private readonly IValidator<CreateTopic> _validator;
    private readonly IBoardReadingService _boardReadingService;
    private readonly IIntentionManager _intentionManager;
    private readonly ITopicFactory _topicFactory;
    private readonly ITopicCreatingRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IInvokedEventProducer _invokedEventProducer;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public TopicCreatingService(
        IValidator<CreateTopic> validator,
        IBoardReadingService boardReadingService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        ITopicFactory topicFactory,
        ITopicCreatingRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IInvokedEventProducer invokedEventProducer)
    {
        _validator = validator;
        _boardReadingService = boardReadingService;
        _intentionManager = intentionManager;
        _topicFactory = topicFactory;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _invokedEventProducer = invokedEventProducer;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<Topic> CreateTopic(CreateTopic createTopic, CancellationToken ct = default)
    {
        using var activity = DmActivitySource.Source.StartActivity("CreateTopic");
        activity?.SetTag("board.title", createTopic.BoardTitle);

        await _validator.ValidateAndThrowAsync(createTopic, ct);

        var board = await _boardReadingService.GetBoard(createTopic.BoardTitle);
        _intentionManager.ThrowIfForbidden(ForumIntention.CreateTopic, board);

        var topicToCreate = _topicFactory.Create(board.Id, _identityProvider.Current.User.UserId, createTopic);
        var topic = await _repository.Create(topicToCreate, ct);

        await Task.WhenAll(
            _invokedEventProducer.Send(EventType.NewForumTopic, topic.Id),
            _unreadCountersRepository.Create(topic.Id, board.Id, UnreadEntryType.Message));

        return topic;
    }
}