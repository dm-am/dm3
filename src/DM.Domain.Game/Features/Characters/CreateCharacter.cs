using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Characters;
/// <summary>
/// DTO model for new character
/// </summary>
public class CreateCharacter
{
    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }
    /// Character name
    public string Name { get; set; } = null!;
    /// Character is NPC
    public bool IsNpc { get; set; }
    /// Character access policy
    public CharacterAccessPolicy AccessPolicy { get; set; }
    /// Initial character status (set by service based on game roles)
    public CharacterStatus InitialStatus { get; set; }
    /// Character attributes
    public IEnumerable<CharacterAttribute> Attributes { get; set; } = [];
}
