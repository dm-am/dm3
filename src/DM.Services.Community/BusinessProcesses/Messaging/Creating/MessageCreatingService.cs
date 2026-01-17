using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Community.BusinessProcesses.Messaging.Reading;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Tracing;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;
using DbConversation = DM.Services.DataAccess.BusinessObjects.Messaging.Conversation;

namespace DM.Services.Community.BusinessProcesses.Messaging.Creating;

/// <inheritdoc />
internal class MessageCreatingService : IMessageCreatingService
{
    private readonly IConversationReadingService _conversationReadingService;
    private readonly IValidator<CreateMessage> _validator;
    private readonly IIntentionManager _intentionManager;
    private readonly IMessageFactory _factory;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IMessageCreatingRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IInvokedEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public MessageCreatingService(
        IConversationReadingService conversationReadingService,
        IValidator<CreateMessage> validator,
        IIntentionManager intentionManager,
        IMessageFactory factory,
        IUpdateBuilderFactory updateBuilderFactory,
        IMessageCreatingRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IInvokedEventProducer producer,
        IIdentityProvider identityProvider)
    {
        _conversationReadingService = conversationReadingService;
        _validator = validator;
        _intentionManager = intentionManager;
        _factory = factory;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _producer = producer;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<Message> Create(CreateMessage createMessage, CancellationToken ct = default)
    {
        using var activity = DmActivitySource.Source.StartActivity("CreateMessage");
        activity?.SetTag("conversation.id", createMessage.ConversationId);

        await _validator.ValidateAndThrowAsync(createMessage, ct);
        var conversation = await _conversationReadingService.Get(createMessage.ConversationId);
        _intentionManager.ThrowIfForbidden(ConversationIntention.CreateMessage, conversation);

        var message = _factory.Create(createMessage, _identityProvider.Current.User.UserId);
        var updateConversation = _updateBuilderFactory.Create<DbConversation>(conversation.Id)
            .Field(c => c.LastMessageId, message.MessageId);

        var result = await _repository.Create(message, updateConversation, ct);
        await _unreadCountersRepository.IncrementExcluding(
            conversation.Id, UnreadEntryType.Message, _identityProvider.Current.User.UserId);
        await _producer.Send(EventType.NewMessage, message.MessageId);

        return result;
    }
}