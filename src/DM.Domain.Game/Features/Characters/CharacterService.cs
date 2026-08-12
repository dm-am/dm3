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

/// <summary>
/// Unified service for character CRUD operations
/// </summary>
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
        _intentionManager.ThrowIfForbidden(GameIntention.CreateCharacter, game);

        var currentUserId = _identityProvider.Current.User.UserId;

        // The blacklist closes writing: reading this game is open to the user it
        // blacklisted, applying to it with a character is not.
        if (game.IsBlacklisted(currentUserId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.BlacklistedFromGame);
        }

        var gameRoles = game.GetRoles(currentUserId);

        // Master and assistant characters should be created in Active status
        var initialStatus = gameRoles.HasEditAccess()
            ? CharacterStatus.Active
            : CharacterStatus.UnderReview;

        // Only master and assistant are allowed to create NPCs
        var isNpc = createCharacter.IsNpc && gameRoles.HasEditAccess();

        var entity = new CreateCharacterEntity
        {
            CharacterId = _guidFactory.Create(),
            GameId = createCharacter.GameId,
            AuthorId = isNpc ? null : currentUserId,
            Name = createCharacter.Name.Trim(),
            IsNpc = isNpc,
            AccessPolicy = createCharacter.AccessPolicy,
            InitialStatus = initialStatus,
            Attributes = createCharacter.Attributes.Select(a => new CharacterAttributeInput
            {
                Id = a.Id,
                Value = a.Value
            }),
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
        var characters = (await _repository.GetCharacters(gameId)).ToArray();
        await _attributeValueFiller.Fill(characters, game, _identityProvider.Current.User.UserId);
        return characters;
    }

    public async Task<Character> GetAsync(Guid characterId)
    {
        var character = await _repository.FindCharacter(characterId);
        if (character == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CharacterNotFound);
        }

        var game = await _gameService.GetAsync(character.GameId);
        await _attributeValueFiller.Fill(new[] { character }, game, _identityProvider.Current.User.UserId);
        return character;
    }

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
        var characterToUpdate = await _repository.GetForUpdate(updateCharacter.CharacterId);
        if (characterToUpdate == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CharacterNotFound);
        }

        _intentionManager.ThrowIfForbidden(CharacterIntention.Edit, characterToUpdate);

        // A field the caller may not set is refused, not dropped. Asking IsAllowed
        // and leaving the value out answered 200 with the character unchanged,
        // which is what success looks like. Asked only when the request actually
        // changes the field: the client round-trips the whole character, and a value
        // it already has is nobody's attempt at anything.
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

        var invokedEvents = new List<EventType> { EventType.ChangedCharacter };

        var attributeInputs = await BuildAttributeInputs(characterToUpdate, updateCharacter.Attributes);

        // No status here: a character's place in the game is moved by
        // ChangeStatusAsync. This method used to move it through a target status plus
        // three flags, a pair the intent had to be guessed from, and a combination
        // that guessed wrong answered 500. The right was checked through IsAllowed as
        // well: a forbidden change fell through silently and the answer was 200 with
        // the old status.
        var entity = new UpdateCharacterEntity
        {
            CharacterId = updateCharacter.CharacterId,
            Name = updateCharacter.Name?.Trim(),
            IsNpc = isNpc,
            AccessPolicy = accessPolicy,
            Attributes = attributeInputs
            // Modification tracking is handled via Edit history, not inline ModifiedUtc
        };

        var character = await _repository.Update(entity);
        await _producer.SendAsync(invokedEvents, updateCharacter.CharacterId);

        return character;
    }

    /// <inheritdoc />
    public async Task<Character> ChangeStatusAsync(
        Guid characterId, CharacterStatusTransition transition)
    {
        var character = await _repository.GetForUpdate(characterId);
        if (character == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CharacterNotFound);
        }

        var entity = new UpdateCharacterEntity { CharacterId = characterId };
        EventType statusEvent;

        switch (transition)
        {
            case CharacterStatusTransition.Accept:
                RequireStatus(character, transition,
                    CharacterStatus.UnderReview, CharacterStatus.Declined);
                _intentionManager.ThrowIfForbidden(CharacterIntention.Accept, character);
                entity.Status = CharacterStatus.Active;
                statusEvent = EventType.StatusCharacterAccepted;
                break;

            case CharacterStatusTransition.Decline:
                RequireStatus(character, transition, CharacterStatus.UnderReview);
                _intentionManager.ThrowIfForbidden(CharacterIntention.Decline, character);
                entity.Status = CharacterStatus.Declined;
                statusEvent = EventType.StatusCharacterDeclined;
                break;

            case CharacterStatusTransition.Kill:
                RequireStatus(character, transition, CharacterStatus.Active);
                _intentionManager.ThrowIfForbidden(CharacterIntention.Kill, character);
                entity.Status = CharacterStatus.Retired;
                entity.IsDead = true;
                statusEvent = EventType.StatusCharacterDied;
                break;

            case CharacterStatusTransition.Exile:
                RequireStatus(character, transition, CharacterStatus.Active);
                _intentionManager.ThrowIfForbidden(CharacterIntention.Exile, character);
                entity.Status = CharacterStatus.Retired;
                entity.IsPlayerExiled = true;
                statusEvent = EventType.StatusCharacterExiled;
                break;

            case CharacterStatusTransition.Leave:
                RequireStatus(character, transition, CharacterStatus.Active);
                _intentionManager.ThrowIfForbidden(CharacterIntention.Leave, character);
                entity.Status = CharacterStatus.Retired;
                entity.IsPlayerLeft = true;
                statusEvent = EventType.StatusCharacterLeft;
                break;

            case CharacterStatusTransition.Resurrect:
                RequireStatus(character, transition, CharacterStatus.Retired);
                _intentionManager.ThrowIfForbidden(CharacterIntention.Resurrect, character);
                entity.Status = CharacterStatus.Active;
                statusEvent = EventType.StatusCharacterResurrected;
                break;

            case CharacterStatusTransition.Return:
                RequireStatus(character, transition, CharacterStatus.Retired);
                _intentionManager.ThrowIfForbidden(CharacterIntention.Return, character);
                entity.Status = CharacterStatus.Active;
                statusEvent = EventType.StatusCharacterReturned;
                break;

            default:
                throw new HttpException(HttpStatusCode.BadRequest, RefusalMessage.UnknownStatusTransition);
        }

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

        // A player who has lost their last active character stays with the game as a
        // reader rather than dropping out of it entirely.
        if (entity.Status != CharacterStatus.Active &&
            character.Status == CharacterStatus.Active &&
            !character.IsNpc)
        {
            var hasOtherActive = await _repository.HasOtherActiveCharacters(
                character.GameId, character.UserId, characterId);

            if (!hasOtherActive)
            {
                // The player who lost the character, not whoever sent the request:
                // the lead who kills or exiles used to become the reader of his own
                // game while the player dropped out of the game list.
                await _subscriptionService.SubscribeUserAsync(character.GameId, character.UserId);
            }
        }

        return updated;
    }

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

    /// <summary>
    /// Builds the attribute inputs for an update, protecting hidden attribute
    /// values against a redacted round-trip. A submitter whose read redacted the
    /// hidden values (anyone who is neither owner nor game lead) must never
    /// create, overwrite, or blank them: their hidden inputs are dropped and the
    /// stored values are restored so they survive the save.
    /// </summary>
    private async Task<IEnumerable<CharacterAttributeInput>?> BuildAttributeInputs(
        CharacterToUpdate character,
        IEnumerable<Games.CharacterAttribute>? submitted)
    {
        if (submitted == null)
        {
            return null;
        }

        var inputs = submitted
            .Select(a => new CharacterAttributeInput { Id = a.Id, Value = a.Value })
            .ToList();

        if (inputs.Count == 0)
        {
            return inputs;
        }

        var game = await _gameService.GetAsync(character.GameId);
        if (!game.AttributeSchemaId.HasValue)
        {
            return inputs;
        }

        var currentUserId = _identityProvider.Current.User.UserId;
        var canSeeHidden = game.GetRoles(currentUserId).HasEditAccess() ||
                           character.UserId == currentUserId;
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
            foreach (var attribute in stored.Attributes.Where(a => hiddenSpecIds.Contains(a.Id)))
            {
                inputs.Add(new CharacterAttributeInput { Id = attribute.Id, Value = attribute.Value });
            }
        }

        return inputs;
    }

    #endregion

    #region Delete

    public async Task DeleteAsync(Guid characterId)
    {
        var character = await _repository.GetForUpdate(characterId);
        if (character == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CharacterNotFound);
        }

        _intentionManager.ThrowIfForbidden(CharacterIntention.Delete, character);

        await _repository.Delete(characterId, _identityProvider.Current.User.UserId);
        await _unreadCountersRepository.DecrementAsync(character.GameId, UnreadEntryType.Character, character.CreatedUtc);
        await _producer.SendAsync(EventType.DeletedCharacter, characterId);
    }

    #endregion
}
