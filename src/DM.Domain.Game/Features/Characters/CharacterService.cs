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
    private readonly ICharacterIntentionConverter _intentionConverter;
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
        ICharacterIntentionConverter intentionConverter,
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
        _intentionConverter = intentionConverter;
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

        // Check blacklist
        if (game.BlacklistedUsers.Any(b => b.UserId == currentUserId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, "You are blacklisted from this game");
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
            throw new HttpException(HttpStatusCode.NotFound, "Character not found");
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
        _intentionManager.ThrowIfForbidden(CharacterIntention.Edit, characterToUpdate);

        // Check privacy settings permission
        CharacterAccessPolicy? accessPolicy = null;
        if (_intentionManager.IsAllowed(CharacterIntention.EditPrivacySettings))
        {
            accessPolicy = updateCharacter.AccessPolicy;
        }

        // Check master settings permission
        bool? isNpc = null;
        if (_intentionManager.IsAllowed(CharacterIntention.EditMasterSettings))
            isNpc = updateCharacter.IsNpc;

        var invokedEvents = new List<EventType> { EventType.ChangedCharacter };

        CharacterStatus? status = null;
        bool? isDead = null;
        bool? isPlayerLeft = null;
        bool? isPlayerExiled = null;

        // Handle status change with proper authorization
        if (updateCharacter.Status.HasValue && updateCharacter.Status != characterToUpdate.Status)
        {
            var dead = updateCharacter.IsDead ?? characterToUpdate.IsDead;
            var left = updateCharacter.IsPlayerLeft ?? characterToUpdate.IsPlayerLeft;
            var (intention, eventType) = _intentionConverter.Convert(
                characterToUpdate.Status, updateCharacter.Status.Value, dead, left);

            if (_intentionManager.IsAllowed(intention, characterToUpdate))
            {
                invokedEvents.Add(eventType);
                status = updateCharacter.Status;
                isDead = updateCharacter.IsDead;
                isPlayerLeft = updateCharacter.IsPlayerLeft;
                isPlayerExiled = updateCharacter.IsPlayerExiled;

                // Clear flags when returning to active
                if (updateCharacter.Status == CharacterStatus.Active &&
                    characterToUpdate.Status == CharacterStatus.Retired)
                {
                    isDead = false;
                    isPlayerLeft = false;
                    isPlayerExiled = false;
                }
            }
        }

        var attributeInputs = await BuildAttributeInputs(characterToUpdate, updateCharacter.Attributes);

        var entity = new UpdateCharacterEntity
        {
            CharacterId = updateCharacter.CharacterId,
            Status = status,
            IsDead = isDead,
            IsPlayerLeft = isPlayerLeft,
            IsPlayerExiled = isPlayerExiled,
            Name = updateCharacter.Name?.Trim(),
            IsNpc = isNpc,
            AccessPolicy = accessPolicy,
            Attributes = attributeInputs
            // Modification tracking is handled via Edit history, not inline ModifiedUtc
        };

        var character = await _repository.Update(entity);
        await _producer.SendAsync(invokedEvents, updateCharacter.CharacterId);

        // Auto-subscribe as Reader when player loses all active characters
        if (status.HasValue &&
            characterToUpdate.Status == CharacterStatus.Active &&
            status.Value != CharacterStatus.Active &&
            !characterToUpdate.IsNpc)
        {
            var hasOtherActive = await _repository.HasOtherActiveCharacters(
                characterToUpdate.GameId,
                characterToUpdate.UserId,
                updateCharacter.CharacterId);

            if (!hasOtherActive)
            {
                await _subscriptionService.SubscribeAsync(characterToUpdate.GameId);
            }
        }

        return character;
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
        _intentionManager.ThrowIfForbidden(CharacterIntention.Delete, character);

        await _repository.Delete(characterId);
        await _unreadCountersRepository.DecrementAsync(character.GameId, UnreadEntryType.Character, character.CreatedUtc);
        await _producer.SendAsync(EventType.DeletedCharacter, characterId);
    }

    #endregion
}
