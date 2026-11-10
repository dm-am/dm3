using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Features.Subscriptions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using DM.Domain.Core.Events;
using FluentValidation;

namespace DM.Domain.Game.Features.Characters;

internal class CharacterService : ICharacterService
{
    private readonly IValidator<CreateCharacter> _createValidator;
    private readonly IValidator<UpdateCharacter> _updateValidator;
    private readonly IGameService _gameService;
    private readonly IIntentionManager _intentionManager;
    private readonly ICharacterRepository _repository;
    private readonly ICharacterAttributeValueFiller _attributeValueFiller;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IGameSubscriptionService _subscriptionService;
    private readonly IEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CharacterService(
        IValidator<CreateCharacter> createValidator,
        IValidator<UpdateCharacter> updateValidator,
        IGameService gameService,
        IIntentionManager intentionManager,
        ICharacterRepository repository,
        ICharacterAttributeValueFiller attributeValueFiller,
        IUnreadCountersRepository unreadCountersRepository,
        IGameSubscriptionService subscriptionService,
        IEventProducer producer,
        IIdentityProvider identityProvider,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _gameService = gameService;
        _intentionManager = intentionManager;
        _repository = repository;
        _attributeValueFiller = attributeValueFiller;
        _unreadCountersRepository = unreadCountersRepository;
        _subscriptionService = subscriptionService;
        _producer = producer;
        _identityProvider = identityProvider;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    #region Create

    public async Task<Character> CreateAsync(CreateCharacter createCharacter)
    {
        await _createValidator.ValidateAndThrowAsync(createCharacter);
        var game = await _gameService.GetAsync(createCharacter.GameId);
        var currentUserId = _identityProvider.Current.User.UserId;

        // The intention carries the same rule; asked first, this one names the reason.
        game.ThrowIfBlacklisted(currentUserId);
        _intentionManager.ThrowIfForbidden(GameIntention.CreateCharacter, game);

        var gameRoles = game.GetRoles(currentUserId);

        var initialStatus = gameRoles.HasEditAccess()
            ? CharacterStatus.Active
            : CharacterStatus.UnderReview;

        var isNpc = createCharacter.IsNpc && gameRoles.HasEditAccess();

        var entity = new CreateCharacterEntity
        {
            CharacterId = _guidFactory.Create(),
            GameId = createCharacter.GameId,
            AuthorId = isNpc ? null : currentUserId,
            Name = createCharacter.Name,
            IsNpc = isNpc,
            AccessPolicy = createCharacter.AccessPolicy,
            InitialStatus = initialStatus,
            Attributes = createCharacter.Attributes.Select(ToInput),
            CreatedUtc = _dateTimeProvider.Now
        };

        var createdCharacter = await _repository.Create(entity);
        await _unreadCountersRepository.IncrementAsync(createCharacter.GameId, UnreadEntryType.Character);
        await _producer.SendAsync(EventType.NewCharacter, createdCharacter.Id);

        return createdCharacter;
    }

    #endregion

    #region Read

    public async Task<IEnumerable<Character>> GetAllAsync(Guid gameId)
    {
        var game = await _gameService.GetAsync(gameId);
        var characters = (await _repository.GetCharacters(gameId))
            .Where(IsVisible)
            .ToArray();
        await _attributeValueFiller.Fill(characters, game, _identityProvider.Current.User.UserId);
        return characters;
    }

    public async Task<bool> MayEditAsync(Guid characterId) =>
        await _repository.GetForUpdate(characterId) is { } character &&
        _intentionManager.IsAllowed(CharacterIntention.Edit, character);

    public async Task<Character> GetAsync(Guid characterId)
    {
        // A direct link does not tell a declined application from an unknown identifier.
        var character = await _repository.FindCharacter(characterId);
        if (character == null || !IsVisible(character))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CharacterNotFound);
        }

        var game = await HiddenContainerRefusal.AsNotFound(RefusalMessage.CharacterNotFound,
            () => _gameService.GetAsync(character.GameId));

        await _attributeValueFiller.Fill(new[] { character }, game, _identityProvider.Current.User.UserId);
        return character;
    }

    // An application under review is seen by everybody; a declined one by nobody, its author and
    // the leads included.
    private static bool IsVisible(Character character) =>
        character.Status != CharacterStatus.Declined;

    public async Task MarkAsReadAsync(Guid gameId)
    {
        await _gameService.GetAsync(gameId);
        await _unreadCountersRepository.FlushAsync(_identityProvider.Current.User.UserId,
            UnreadEntryType.Character, gameId);
    }

    #endregion

    #region Update

    public async Task<Character> UpdateAsync(UpdateCharacter updateCharacter)
    {
        await _updateValidator.ValidateAndThrowAsync(updateCharacter);
        var characterToUpdate = await _repository.GetForUpdateOrThrow(updateCharacter.CharacterId);
        _intentionManager.ThrowIfForbidden(CharacterIntention.Edit, characterToUpdate);

        // Editing one's own application is applying again, which the blacklist closes.
        var currentUserId = _identityProvider.Current.User.UserId;
        if (characterToUpdate.AuthorId == currentUserId && IsApplication(characterToUpdate))
        {
            var game = await _gameService.GetAsync(characterToUpdate.GameId);
            game.ThrowIfBlacklisted(currentUserId);
        }

        // A field the caller may not set is refused, not dropped, and only when the request changes it:
        // the client round-trips the whole character.
        CharacterAccessPolicy? accessPolicy = null;
        if (updateCharacter.AccessPolicy.HasValue &&
            updateCharacter.AccessPolicy != characterToUpdate.AccessPolicy)
        {
            _intentionManager.ThrowIfForbidden(CharacterIntention.EditPrivacySettings);
            accessPolicy = updateCharacter.AccessPolicy;
        }

        bool? isNpc = null;
        if (updateCharacter.IsNpc.HasValue && updateCharacter.IsNpc != characterToUpdate.IsNpc)
        {
            _intentionManager.ThrowIfForbidden(CharacterIntention.EditMasterSettings);
            isNpc = updateCharacter.IsNpc;
        }

        var attributeInputs = await BuildAttributeInputs(characterToUpdate, updateCharacter.Attributes);
        // No status here: ChangeStatusAsync moves the character's place in the game.
        var entity = new UpdateCharacterEntity
        {
            CharacterId = updateCharacter.CharacterId,
            Name = updateCharacter.Name,
            IsNpc = isNpc,
            AccessPolicy = accessPolicy,
            Attributes = attributeInputs
        };

        var character = await _repository.Update(entity);
        await _producer.SendAsync(EventType.ChangedCharacter, updateCharacter.CharacterId);

        return character;
    }

    /// <inheritdoc />
    public async Task<Character> ChangeStatusAsync(
        Guid characterId, CharacterStatusTransition transition)
    {
        var character = await _repository.GetForUpdateOrThrow(characterId);
        var entity = new UpdateCharacterEntity { CharacterId = characterId };
        var statusEvent = ApplyTransition(character, transition, entity);

        // Coming back to the game clears all three reasons for leaving: otherwise a
        // revived character stays marked dead and a second revival is impossible.
        if (entity.Status == CharacterStatus.Active && character.Status == CharacterStatus.Retired)
        {
            entity.IsDead = false;
            entity.IsPlayerLeft = false;
            entity.IsPlayerExiled = false;
        }

        var updated = await _repository.Update(entity);
        await _producer.SendAsync(
            new List<EventType> { EventType.ChangedCharacter, statusEvent }, characterId);

        await KeepPlayerAsReader(character, entity);

        return updated;
    }

    /// <remarks>
    /// Refused first for the status it starts from, then for the right to make it.
    /// </remarks>
    private EventType ApplyTransition(
        CharacterToUpdate character, CharacterStatusTransition transition, UpdateCharacterEntity entity)
    {
        switch (transition)
        {
            case CharacterStatusTransition.Accept:
                RequireStatus(character, transition,
                    CharacterStatus.UnderReview, CharacterStatus.Declined);
                _intentionManager.ThrowIfForbidden(CharacterIntention.Accept, character);
                entity.Status = CharacterStatus.Active;
                return EventType.StatusCharacterAccepted;

            case CharacterStatusTransition.Decline:
                RequireStatus(character, transition, CharacterStatus.UnderReview);
                _intentionManager.ThrowIfForbidden(CharacterIntention.Decline, character);
                entity.Status = CharacterStatus.Declined;
                return EventType.StatusCharacterDeclined;

            case CharacterStatusTransition.Kill:
                RequireStatus(character, transition, CharacterStatus.Active);
                _intentionManager.ThrowIfForbidden(CharacterIntention.Kill, character);
                entity.Status = CharacterStatus.Retired;
                entity.IsDead = true;
                return EventType.StatusCharacterDied;

            case CharacterStatusTransition.Exile:
                RequireStatus(character, transition, CharacterStatus.Active);
                _intentionManager.ThrowIfForbidden(CharacterIntention.Exile, character);
                entity.Status = CharacterStatus.Retired;
                entity.IsPlayerExiled = true;
                return EventType.StatusCharacterExiled;

            case CharacterStatusTransition.Leave:
                RequireStatus(character, transition, CharacterStatus.Active);
                _intentionManager.ThrowIfForbidden(CharacterIntention.Leave, character);
                entity.Status = CharacterStatus.Retired;
                entity.IsPlayerLeft = true;
                return EventType.StatusCharacterLeft;

            case CharacterStatusTransition.Resurrect:
                RequireStatus(character, transition, CharacterStatus.Retired);
                _intentionManager.ThrowIfForbidden(CharacterIntention.Resurrect, character);
                entity.Status = CharacterStatus.Active;
                return EventType.StatusCharacterResurrected;

            case CharacterStatusTransition.Readmit:
                RequireStatus(character, transition, CharacterStatus.Retired);
                _intentionManager.ThrowIfForbidden(CharacterIntention.Readmit, character);
                entity.Status = CharacterStatus.Active;
                return EventType.StatusCharacterReadmitted;

            default:
                throw new HttpException(HttpStatusCode.BadRequest, RefusalMessage.UnknownStatusTransition);
        }
    }

    // A player who lost his last active character stays as a reader, a blacklisted one too:
    // following the game is reading.
    private async Task KeepPlayerAsReader(CharacterToUpdate character, UpdateCharacterEntity entity)
    {
        if (entity.Status != CharacterStatus.Active &&
            character.Status == CharacterStatus.Active &&
            !character.IsNpc)
        {
            var hasOtherActive = await _repository.HasOtherActiveCharacters(
                character.GameId, character.AuthorId, entity.CharacterId);

            if (!hasOtherActive)
            {
                // The player who lost the character, not whoever sent the request.
                await _subscriptionService.SubscribeUserAsync(character.GameId, character.AuthorId);
            }
        }
    }

    private static bool IsApplication(CharacterToUpdate character) =>
        character.Status is CharacterStatus.UnderReview or CharacterStatus.Declined;

    private static void RequireStatus(
        CharacterToUpdate character, CharacterStatusTransition transition,
        params CharacterStatus[] allowed)
    {
        if (!allowed.Contains(character.Status))
        {
            throw new HttpException(HttpStatusCode.BadRequest,
                RefusalMessage.IllegalStatusTransition(transition, character.Status));
        }
    }

    // A submitter whose read redacted the hidden values must not blank them: his hidden inputs are
    // dropped and the stored values restored.
    private async Task<IEnumerable<CharacterAttributeInput>?> BuildAttributeInputs(
        CharacterToUpdate character,
        IEnumerable<CharacterAttribute>? submitted)
    {
        if (submitted == null)
        {
            return null;
        }

        var inputs = submitted.Select(ToInput).ToList();

        if (inputs.Count == 0)
        {
            return inputs;
        }

        var game = await _gameService.GetAsync(character.GameId);
        var currentUserId = _identityProvider.Current.User.UserId;
        var canSeeHidden = game.GetRoles(currentUserId).HasEditAccess() ||
                           character.AuthorId == currentUserId;
        if (canSeeHidden)
        {
            return inputs;
        }

        var schema = await _repository.GetGameSchema(game.Id);
        var hiddenSpecIds = schema.Specifications
            .Where(s => s.IsHidden)
            .Select(s => s.Id)
            .ToHashSet();
        if (hiddenSpecIds.Count == 0)
        {
            return inputs;
        }

        inputs.RemoveAll(a => hiddenSpecIds.Contains(a.Id));

        var stored = await _repository.FindCharacter(character.Id);
        if (stored != null)
        {
            inputs.AddRange(stored.Attributes.Where(a => hiddenSpecIds.Contains(a.Id)).Select(ToInput));
        }

        return inputs;
    }

    private static CharacterAttributeInput ToInput(CharacterAttribute attribute) =>
        new() { Id = attribute.Id, Value = attribute.Value };

    #endregion

    #region Delete

    public async Task DeleteAsync(Guid characterId)
    {
        var character = await _repository.GetForUpdateOrThrow(characterId);
        _intentionManager.ThrowIfForbidden(CharacterIntention.Delete, character);

        await _repository.Delete(characterId, _identityProvider.Current.User.UserId);
        await _unreadCountersRepository.DecrementAsync(character.GameId, UnreadEntryType.Character, character.CreatedUtc);
        await _producer.SendAsync(EventType.DeletedCharacter, characterId);
    }

    #endregion
}
