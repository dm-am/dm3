using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.BbRendering;

namespace DM.Web.API.Features.Game.Characters;

/// <summary>
/// DTO model for game character with full details
/// </summary>
public class CharacterDetails : Character
{
    /// <summary>
    /// Character attributes
    /// </summary>
    public IEnumerable<CharacterAttribute> Attributes { get; set; } = [];

    /// <summary>
    /// Character privacy settings
    /// </summary>
    public CharacterPrivacySettings Privacy { get; set; } = null!;
}
