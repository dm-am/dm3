using System;
using System.Collections.Generic;

namespace DM.Web.API.Features.Game.Characters;

/// <summary>
/// API DTO for editing an existing character
/// </summary>
/// <remarks>
/// The editable content only; the place of a character in the game changes
/// through POST /v1/characters/{id}/status.
/// </remarks>
public class UpdateCharacterRequest
{
    /// <summary>
    /// Character name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Privacy settings. An omitted block leaves them unchanged — which is the
    /// distinction the previous shape could not express.
    /// </summary>
    public CharacterPrivacySettings? Privacy { get; set; }

    /// <summary>
    /// Attribute values
    /// </summary>
    public IEnumerable<UpdateCharacterAttribute> Attributes { get; set; } = [];
}

/// <summary>
/// A single attribute value submitted with a character edit
/// </summary>
/// <remarks>
/// Only the specification id and the raw value reach the domain.
/// </remarks>
public class UpdateCharacterAttribute
{
    /// <summary>
    /// Specification identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Raw attribute value, BBCode included
    /// </summary>
    public string? Value { get; set; }
}
