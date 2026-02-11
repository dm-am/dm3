using System.Threading;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Creating;

/// <inheritdoc />
internal class GlobalChatEventCreatingService : IGlobalChatEventCreatingService
{
    private readonly IValidator<CreateGlobalChatEvent> _validator;
    private readonly IIntentionManager _intentionManager;
    private readonly IGlobalChatEventFactory _factory;
    private readonly IGlobalChatEventCreatingRepository _repository;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public GlobalChatEventCreatingService(
        IValidator<CreateGlobalChatEvent> validator,
        IIntentionManager intentionManager,
        IGlobalChatEventFactory factory,
        IGlobalChatEventCreatingRepository repository,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _intentionManager = intentionManager;
        _factory = factory;
        _repository = repository;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<GlobalChatEvent> Create(CreateGlobalChatEvent createGlobalChatEvent, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(createGlobalChatEvent, ct).ConfigureAwait(false);
        _intentionManager.ThrowIfForbidden(GlobalChatEventIntention.Create);

        var userId = _identityProvider.Current.User.UserId;
        var GlobalChatEvent = _factory.Create(createGlobalChatEvent, userId);
        var creatorParticipant = _factory.CreateParticipant(GlobalChatEvent.GlobalChatEventId, userId, isOrganizer: true);

        return await _repository.Create(GlobalChatEvent, creatorParticipant, ct).ConfigureAwait(false);
    }
}
