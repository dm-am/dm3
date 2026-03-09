using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Game.Characters;

/// <summary>
/// DTO model for game character with full details
/// </summary>
public class CharacterDetails : Character
{
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
    /// Character alignment
    /// </summary>
    public Alignment? Alignment { get; set; }

    /// <summary>
    /// Character attributes
    /// </summary>
    public IEnumerable<CharacterAttribute> Attributes { get; set; } = [];

    /// <summary>
    /// Character privacy settings
    /// </summary>
    public CharacterPrivacySettings Privacy { get; set; } = null!;
}
