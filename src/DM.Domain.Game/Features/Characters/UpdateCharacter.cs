using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Characters;

/// <remarks>
/// Content only: <see cref="ICharacterService.ChangeStatusAsync"/> changes the character's place in the game.
/// </remarks>
public class UpdateCharacter
{
    public Guid CharacterId { get; set; }

    public string? Name { get; set; }

    public bool? IsNpc { get; set; }

    public CharacterAccessPolicy? AccessPolicy { get; set; }

    public IEnumerable<CharacterAttribute> Attributes { get; set; } = [];
}
