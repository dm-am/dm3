using System.Collections.Generic;
using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Dto.Games;

/// <summary>
/// DTO model for game character with full details
/// </summary>
public class CharacterDetails : Character
{
    /// <summary>
    /// Character appearance
    /// </summary>
    public string Appearance { get; set; }

    /// <summary>
    /// Character temper
    /// </summary>
    public string Temper { get; set; }

    /// <summary>
    /// Character story
    /// </summary>
    public string Story { get; set; }

    /// <summary>
    /// Character skills
    /// </summary>
    public string Skills { get; set; }

    /// <summary>
    /// Character inventory
    /// </summary>
    public string Inventory { get; set; }

    /// <summary>
    /// Character alignment
    /// </summary>
    public Alignment? Alignment { get; set; }

    /// <summary>
    /// Character attributes
    /// </summary>
    public IEnumerable<CharacterAttribute> Attributes { get; set; }

    /// <summary>
    /// Character privacy settings
    /// </summary>
    public CharacterPrivacySettings Privacy { get; set; }
}
