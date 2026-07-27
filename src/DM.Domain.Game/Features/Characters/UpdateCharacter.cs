using DM.Domain.Game.Features.Games;
using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Characters;
/// <summary>
/// DTO model for character updating
/// </summary>
public class UpdateCharacter
{
    /// <summary>
    /// Character identifier
    /// </summary>
    public Guid CharacterId { get; set; }
    /// Character status
    public CharacterStatus? Status { get; set; }
    /// Character died in game (only when Status = Retired)
    public bool? IsDead { get; set; }
    /// Player left the game voluntarily (only when Status = Retired)
    public bool? IsPlayerLeft { get; set; }
    /// Player was exiled from the game by GM (only when Status = Retired)
    public bool? IsPlayerExiled { get; set; }
    /// Character name
    public string Name { get; set; } = null!;
    /// Character is NPC
    public bool? IsNpc { get; set; }
    /// Character access policy
    public CharacterAccessPolicy? AccessPolicy { get; set; }
    /// Character attributes
    public IEnumerable<CharacterAttribute> Attributes { get; set; } = [];
}
