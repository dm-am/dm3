using DM.Services.Core.Dto.Enums;
using DM.Services.Game.Authorization;

namespace DM.Services.Game.BusinessProcesses.Characters.Updating;

/// <inheritdoc />
internal class CharacterIntentionConverter : ICharacterIntentionConverter
{
    /// <inheritdoc />
    public (CharacterIntention intention, EventType eventType) Convert(
        CharacterStatus statusFrom, CharacterStatus statusTo,
        bool isDead = false, bool isPlayerLeft = false) => statusTo switch
    {
        CharacterStatus.Declined => (CharacterIntention.Decline, EventType.StatusCharacterDeclined),
        CharacterStatus.Active when statusFrom == CharacterStatus.UnderReview => (CharacterIntention.Accept,
            EventType.StatusCharacterAccepted),
        CharacterStatus.Active when statusFrom == CharacterStatus.Declined => (CharacterIntention.Accept,
            EventType.StatusCharacterAccepted),
        CharacterStatus.Active when statusFrom == CharacterStatus.Retired && isDead => (CharacterIntention.Resurrect,
            EventType.StatusCharacterResurrected),
        CharacterStatus.Active when statusFrom == CharacterStatus.Retired && isPlayerLeft => (CharacterIntention.Return,
            EventType.StatusCharacterReturned),
        CharacterStatus.Retired when isDead => (CharacterIntention.Kill, EventType.StatusCharacterDied),
        CharacterStatus.Retired when isPlayerLeft => (CharacterIntention.Leave, EventType.StatusCharacterLeft),
        _ => throw new CharacterIntentionConverterException(statusTo)
    };
}