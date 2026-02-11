using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;
using DM.Services.Community.BusinessProcesses.Messaging.Reading;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
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
    private readonly IGlobalChatEventReadingRepository _globalChatEventReadingRepository;
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
        IGlobalChatEventReadingRepository globalChatEventReadingRepository,
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
        _globalChatEventReadingRepository = globalChatEventReadingRepository;
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

        var userId = _identityProvider.Current.User.UserId;
        GlobalChatEvent? activeEvent = null;

        // For global chat, check if there's an active event with restrictions
        if (conversation.Type == ConversationType.Global)
        {
            activeEvent = await _globalChatEventReadingRepository.GetActiveEvent();
            if (activeEvent != null && !activeEvent.IsOpen)
            {
                // Closed event - only participants can send messages
                var isParticipant = activeEvent.Participants?.Any(p => p.User.UserId == userId) ?? false;
                if (!isParticipant)
                {
                    throw new HttpException(HttpStatusCode.Forbidden,
                        "There is a closed chat event in progress. Only event participants can send messages.");
                }
            }
        }

        var message = _factory.Create(createMessage, userId);

        // If there's an active event, link the message to it
        if (activeEvent != null)
        {
            message.GlobalChatEventId = activeEvent.Id;
        }

        var updateConversation = _updateBuilderFactory.Create<DbConversation>(conversation.Id)
            .Field(c => c.LastMessageId, message.MessageId);

        var result = await _repository.Create(message, updateConversation, ct);
        await _unreadCountersRepository.IncrementExcluding(
            conversation.Id, UnreadEntryType.Message, userId);
        await _producer.Send(EventType.NewMessage, message.MessageId);

        return result;
    }
}