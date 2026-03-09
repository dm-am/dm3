using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;

namespace DM.Domain.Game.Features.Characters;

/// <summary>
/// Converter for character status into associated intention
/// </summary>
internal interface ICharacterIntentionConverter
{
    /// <summary>
    /// Convert character status to associated intention
    /// </summary>
    /// <param name="statusFrom">Current status</param>
    /// <param name="statusTo">Desired status</param>
    /// <param name="isDead">Character is dead (when Retired)</param>
    /// <param name="isPlayerLeft">Player left voluntarily (when Retired)</param>
    /// <returns>Authorized intention and invoked event type</returns>
    (CharacterIntention intention, EventType eventType) Convert(
        CharacterStatus statusFrom, CharacterStatus statusTo,
        bool isDead = false, bool isPlayerLeft = false);
}