using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.BusinessObjects.Games.Characters;
using DM.Services.DataAccess.BusinessObjects.Games.Characters.Attributes;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Characters.Updating;
using DM.Services.MessageQueuing.GeneralBus;

namespace DM.Services.Game.BusinessProcesses.Characters.Deleting;

/// <inheritdoc />
internal class CharacterDeletingService : ICharacterDeletingService
{
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly ICharacterUpdatingRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IInvokedEventProducer _producer;

    /// <inheritdoc />
    public CharacterDeletingService(
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        ICharacterUpdatingRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IInvokedEventProducer producer)
    {
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _producer = producer;
    }

    /// <inheritdoc />
    public async Task Delete(Guid characterId)
    {
        var character = await _repository.Get(characterId);
        _intentionManager.ThrowIfForbidden(CharacterIntention.Delete, character);

        var updateCharacter = _updateBuilderFactory.Create<Character>(characterId);
        updateCharacter.Field(c => c.IsRemoved, true);
        var updateAttributes = (await _repository.GetAttributeIds(characterId)).Keys
            .Select(id => _updateBuilderFactory.Create<CharacterAttribute>(id).Delete());

        await _repository.Update(updateCharacter, updateAttributes);
        await _unreadCountersRepository.Decrement(character.GameId, UnreadEntryType.Character, character.CreatedUtc);
        await _producer.Send(EventType.DeletedCharacter, characterId);
    }
}