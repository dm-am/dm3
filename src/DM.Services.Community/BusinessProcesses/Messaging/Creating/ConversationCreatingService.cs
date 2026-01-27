using System.Linq;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Community.BusinessProcesses.Messaging.Reading;
using DM.Services.DataAccess.BusinessObjects.Common;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Messaging.Creating;

/// <inheritdoc />
internal class ConversationCreatingService : IConversationCreatingService
{
    private readonly IValidator<CreateConversation> _validator;
    private readonly IConversationFactory _factory;
    private readonly IConversationReadingRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public ConversationCreatingService(
        IValidator<CreateConversation> validator,
        IConversationFactory factory,
        IConversationReadingRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _factory = factory;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<Conversation> CreateGroup(CreateConversation createConversation)
    {
        await _validator.ValidateAndThrowAsync(createConversation);

        var currentUserId = _identityProvider.Current.User.UserId;
        var allParticipants = createConversation.ParticipantIds
            .Append(currentUserId)
            .Distinct()
            .ToArray();

        var (conversation, conversationLinks) = _factory.CreateGroup(createConversation.Title, allParticipants);
        var result = await _repository.Create(conversation, conversationLinks);

        await _unreadCountersRepository.Create(result.Id, UnreadEntryType.Message, allParticipants);

        return result;
    }
}
