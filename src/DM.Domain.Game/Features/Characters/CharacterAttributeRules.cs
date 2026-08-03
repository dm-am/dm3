using System.Collections.Generic;
using System.Linq;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.Characters;

/// <summary>
/// Shared rules for the attribute values submitted with a character.
/// Reused by both create and update character validators so every persistence
/// path enforces the same invariants.
/// </summary>
internal static class CharacterAttributeRules
{
    /// <summary>
    /// Collect all rule violations for the submitted attribute values.
    /// </summary>
    /// <param name="attributes">Attribute values being persisted (may be null on update)</param>
    /// <returns>Human-readable error messages, empty when valid</returns>
    public static IEnumerable<string> Collect(IEnumerable<CharacterAttribute>? attributes)
    {
        if (attributes == null)
        {
            yield break;
        }

        // One value per specification: the stored rows are unique on
        // (CharacterId, AttributeId) and the update path indexes them by
        // AttributeId. A repeated identifier is a request that cannot be
        // stored — one of the two values would be lost, and before the unique
        // index existed both were written and left the character uneditable.
        var duplicated = attributes
            .GroupBy(a => a.Id)
            .Where(g => g.Skip(1).Any())
            .Select(g => g.Key)
            .ToArray();

        if (duplicated.Length > 0)
        {
            yield return AttributeValidationError.ManyDuplicated(duplicated);
        }
    }
}
