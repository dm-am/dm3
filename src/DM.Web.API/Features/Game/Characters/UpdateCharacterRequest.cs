using System;
using System.Collections.Generic;

namespace DM.Web.API.Features.Game.Characters;

/// <summary>
/// API DTO for editing an existing character
/// </summary>
/// <remarks>
/// Content only, and only the editable part of it. The endpoint used to take
/// CharacterDetails, the response DTO, so the contract asked for an author
/// reference, a picture, post counters and a rating to change a name — and the
/// server's defence against the rest was per-field Ignore() in the mapping
/// profile. That arrangement has already cost one defect: a PATCH with no
/// privacy block mapped to a plain false and demoted an NPC to a player
/// character. A character's place in the game is changed by
/// POST /v1/characters/{id}/status, which names the transition.
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
/// The specification's own title, type and rendered BBCode are the server's
/// answer, not the client's input: only the specification id and the raw value
/// have ever reached the domain.
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
