using System;
using System.Collections.Generic;
using DM.Domain.Game.Features.AttributeSchemas;

namespace DM.Domain.Game.Features.AttributeSchemaTemplates;

/// <summary>
/// A starting character sheet the site offers when a game is created
/// </summary>
/// <remarks>
/// A game takes a copy by value, so changing or hiding a template leaves every game alone.
/// </remarks>
public class AttributeSchemaTemplate
{
    public Guid Id { get; set; }

    /// <summary>
    /// Display name, unique among the templates that are not hidden
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// The one the creation form starts from. Read-only in the representation:
    /// it is moved by an action of its own, never by writing the field.
    /// </summary>
    public bool IsDefault { get; set; }

    public IEnumerable<AttributeSpecification> Specifications { get; set; } = [];
}

public class CreateAttributeSchemaTemplate
{
    public string Title { get; set; } = null!;

    /// <summary>
    /// Every identifier is the server's, so the creation shape carries none.
    /// </summary>
    public IEnumerable<CreateAttributeSpecification> Specifications { get; set; } = [];
}

/// <summary>
/// DTO for replacing an attribute schema template
/// </summary>
/// <remarks>
/// Specification identifiers are handed out anew: nothing addresses them, a game holds its own copy.
/// </remarks>
public class ReplaceAttributeSchemaTemplate : CreateAttributeSchemaTemplate
{
    public Guid TemplateId { get; set; }
}

/// <summary>
/// What the conditional write behind a template removal did
/// </summary>
public enum AttributeSchemaTemplateRemoval
{
    /// <summary>
    /// The template is hidden now
    /// </summary>
    Hidden = 0,

    /// <summary>
    /// The template is the one new games start from, and the site is never left
    /// without one
    /// </summary>
    RefusedAsDefault = 1,

    /// <summary>
    /// No such live template
    /// </summary>
    NotFound = 2
}
