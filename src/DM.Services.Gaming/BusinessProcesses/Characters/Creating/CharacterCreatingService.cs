using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.Gaming.Authorization;
using DM.Services.Gaming.BusinessProcesses.Games.Reading;
using DM.Services.Gaming.Dto;
using DM.Services.Gaming.Dto.Input;
using DM.Services.Gaming.Dto.Output;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;

namespace DM.Services.Gaming.BusinessProcesses.Characters.Creating;

/// <inheritdoc />
internal class CharacterCreatingService : ICharacterCreatingService
{
    private readonly IValidator<CreateCharacter> _validator;
    private readonly IGameReadingService _gameReadingService;
    private readonly IIntentionManager _intentionManager;
    private readonly ICharacterFactory _factory;
    private readonly ICharacterCreatingRepository _creatingRepository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IInvokedEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public CharacterCreatingService(
        IValidator<CreateCharacter> validator,
        IGameReadingService gameReadingService,
        IIntentionManager intentionManager,
        ICharacterFactory factory,
        ICharacterCreatingRepository creatingRepository,
        IUnreadCountersRepository unreadCountersRepository,
        IInvokedEventProducer producer,
        IIdentityProvider identityProvider)
    {
        _validator = validator;
        _gameReadingService = gameReadingService;
        _intentionManager = intentionManager;
        _factory = factory;
        _creatingRepository = creatingRepository;
        _unreadCountersRepository = unreadCountersRepository;
        _producer = producer;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<Character> Create(CreateCharacter createCharacter)
    {
        await _validator.ValidateAndThrowAsync(createCharacter);
        var game = await _gameReadingService.GetGame(createCharacter.GameId);
        _intentionManager.ThrowIfForbidden(GameIntention.CreateCharacter, game);

        var currentUserId = _identityProvider.Current.User.UserId;
        var gameParticipation = game.Participation(currentUserId);

        // Master and assistant characters should be created in Active status
        var initialStatus = gameParticipation.HasFlag(GameParticipation.Authority)
            ? CharacterStatus.Active
            : CharacterStatus.UnderReview;

        // Only master and assistant are allowed to create NPCs
        createCharacter.IsNpc = createCharacter.IsNpc && gameParticipation.HasFlag(GameParticipation.Authority);

        var (character, attributes) = _factory.Create(createCharacter, currentUserId, initialStatus);
        var createdCharacter = await _creatingRepository.Create(character, attributes);

        await _unreadCountersRepository.Increment(createCharacter.GameId, UnreadEntryType.Character);
        await _producer.Send(EventType.NewCharacter, createdCharacter.Id);

        return createdCharacter;
    }
}