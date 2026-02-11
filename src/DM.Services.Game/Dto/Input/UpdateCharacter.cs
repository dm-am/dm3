using System;
using System.Collections.Generic;
using DM.Services.Core.Dto.Enums;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.Dto.Input;

/// <summary>
/// DTO model for character updating
/// </summary>
public class UpdateCharacter
{
    /// <summary>
    /// Character identifier
    /// </summary>
    public Guid CharacterId { get; set; }

    /// <summary>
    /// Character status
    /// </summary>
    public CharacterStatus? Status { get; set; }

    /// <summary>
    /// Character died in game (only when Status = Retired)
    /// </summary>
    public bool? IsDead { get; set; }

    /// <summary>
    /// Player left the game voluntarily (only when Status = Retired)
    /// </summary>
    public bool? IsPlayerLeft { get; set; }

    /// <summary>
    /// Player was exiled from the game by GM (only when Status = Retired)
    /// </summary>
    public bool? IsPlayerExiled { get; set; }

    /// <summary>
    /// Character name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Character race
    /// </summary>
    public string Race { get; set; } = null!;

    /// <summary>
    /// Character class
    /// </summary>
    public string Class { get; set; } = null!;

    /// <summary>
    /// Character alignment
    /// </summary>
    public Alignment? Alignment { get; set; }

    /// <summary>
    /// Character appearance
    /// </summary>
    public string Appearance { get; set; } = null!;

    /// <summary>
    /// Character temper
    /// </summary>
    public string Temper { get; set; } = null!;

    /// <summary>
    /// Character story
    /// </summary>
    public string Story { get; set; } = null!;

    /// <summary>
    /// Character skills
    /// </summary>
    public string Skills { get; set; } = null!;

    /// <summary>
    /// Character inventory
    /// </summary>
    public string Inventory { get; set; } = null!;

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