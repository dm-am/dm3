using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.Characters;

/// <summary>
/// DTO model for character updating
/// </summary>
/// <remarks>
/// Content only. A character's place in the game is changed by
/// <see cref="ICharacterService.ChangeStatusAsync"/>, which takes the transition
/// by name; this model used to carry a target status and three booleans beside it,
/// and the service had to guess which of them meant what.
/// </remarks>
public class UpdateCharacter
{
    /// <summary>
    /// Character identifier
    /// </summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// Character name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Character is NPC
    /// </summary>
    public bool? IsNpc { get; set; }

    /// <summary>
    /// Character access policy
    /// </summary>
    public CharacterAccessPolicy? AccessPolicy { get; set; }

    /// <summary>
    /// Character attributes
    /// </summary>
    public IEnumerable<CharacterAttribute> Attributes { get; set; } = [];
}
