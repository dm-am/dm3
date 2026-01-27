using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Chat.Reading;
using DM.Services.Core.Dto.Enums;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Chat.Creating;

/// <inheritdoc />
internal class ChatCreatingService : IChatCreatingService
{
    private readonly IValidator<CreateChatMessage> _validator;
    private readonly IIntentionManager _intentionManager;
    private readonly IChatMessageFactory _factory;
    private readonly IChatCreatingRepository _repository;
    private readonly IInvokedEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public ChatCreatingService(
        IValidator<CreateChatMessage> validator,
        IIntentionManager intentionManager,
        IChatMessageFactory factory,
        IChatCreatingRepository repository,
        IInvokedEventProducer producer,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _intentionManager = intentionManager;
        _factory = factory;
        _repository = repository;
        _producer = producer;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<ChatMessage> Create(CreateChatMessage createChatMessage)
    {
        await _validator.ValidateAndThrowAsync(createChatMessage);
        _intentionManager.ThrowIfForbidden(ChatIntention.CreateMessage);

        var chatMessage = _factory.Create(createChatMessage, _identityProvider.Current.User.UserId);
        var result = await _repository.Create(chatMessage);
        await _producer.Send(EventType.NewChatMessage, chatMessage.MessageId);

        return result;
    }
}