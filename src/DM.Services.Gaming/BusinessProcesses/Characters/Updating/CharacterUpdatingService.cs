using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Gaming.Authorization;
using DM.Services.Gaming.Dto.Input;
using DM.Services.Gaming.Dto.Output;
using DM.Services.MessageQueuing.GeneralBus;
using FluentValidation;
using DbCharacter = DM.Services.DataAccess.BusinessObjects.Games.Characters.Character;
using DbAttribute = DM.Services.DataAccess.BusinessObjects.Games.Characters.Attributes.CharacterAttribute;

namespace DM.Services.Gaming.BusinessProcesses.Characters.Updating;

/// <inheritdoc />
internal class CharacterUpdatingService : ICharacterUpdatingService
{
    private readonly IValidator<UpdateCharacter> _validator;
    private readonly IIntentionManager _intentionManager;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly ICharacterUpdatingRepository _repository;
    private readonly ICharacterIntentionConverter _intentionConverter;
    private readonly IInvokedEventProducer _producer;

    /// <inheritdoc />
    public CharacterUpdatingService(
        IValidator<UpdateCharacter> validator,
        IIntentionManager intentionManager,
        IDateTimeProvider dateTimeProvider,
        IUpdateBuilderFactory updateBuilderFactory,
        ICharacterUpdatingRepository repository,
        ICharacterIntentionConverter intentionConverter,
        IInvokedEventProducer producer)
    {
        _validator = validator;
        _intentionManager = intentionManager;
        _dateTimeProvider = dateTimeProvider;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _intentionConverter = intentionConverter;
        _producer = producer;
    }

    /// <inheritdoc />
    public async Task<Character> Update(UpdateCharacter updateCharacter)
    {
        await _validator.ValidateAndThrowAsync(updateCharacter);
        var characterToUpdate = await _repository.Get(updateCharacter.CharacterId);

        _intentionManager.ThrowIfForbidden(CharacterIntention.Edit, characterToUpdate);

        var changes = _updateBuilderFactory.Create<DbCharacter>(updateCharacter.CharacterId)
            .MaybeField(c => c.Name, updateCharacter.Name?.Trim())
            .MaybeField(c => c.Race, updateCharacter.Race?.Trim())
            .MaybeField(c => c.Class, updateCharacter.Class?.Trim())
            .MaybeField(c => c.Appearance, updateCharacter.Appearance)
            .MaybeField(c => c.Temper, updateCharacter.Temper)
            .MaybeField(c => c.Story, updateCharacter.Story)
            .MaybeField(c => c.Skills, updateCharacter.Skills)
            .MaybeField(c => c.Inventory, updateCharacter.Inventory);

        var attributeChanges = new IUpdateBuilder<DbAttribute>[0];
        if (updateCharacter.Attributes != null && updateCharacter.Attributes.Any())
        {
            var attributeIdsIndex = await _repository.GetAttributeIds(updateCharacter.CharacterId);
            attributeChanges = updateCharacter.Attributes
                .Where(a => attributeIdsIndex.ContainsKey(a.Id))
                .Select(a => _updateBuilderFactory.Create<DbAttribute>(attributeIdsIndex[a.Id])
                    .Field(aa => aa.Value, a.Value?.Trim()))
                .ToArray();
        }

        if (_intentionManager.IsAllowed(CharacterIntention.EditPrivacySettings))
        {
            changes.MaybeField(c => c.AccessPolicy, updateCharacter.AccessPolicy);
        }

        if (_intentionManager.IsAllowed(CharacterIntention.EditMasterSettings))
        {
            changes.MaybeField(c => c.IsNpc, updateCharacter.IsNpc);
        }

        var invokedEvents = new List<EventType> {EventType.ChangedCharacter};
        if (updateCharacter.Status.HasValue && updateCharacter.Status != characterToUpdate.Status)
        {
            var (intention, eventType) = _intentionConverter.Convert(
                updateCharacter.Status.Value, characterToUpdate.Status);
            if (_intentionManager.IsAllowed(intention, characterToUpdate))
            {
                changes.Field(c => c.Status, updateCharacter.Status.Value);
                invokedEvents.Add(eventType);
            }
        }

        if (changes.HasChanges() || attributeChanges.Any(c => c.HasChanges()))
        {
            changes.Field(c => c.LastUpdateDate, _dateTimeProvider.Now);
        }

        var character = await _repository.Update(changes, attributeChanges);
        await _producer.Send(invokedEvents, updateCharacter.CharacterId);
        return character;
    }
}