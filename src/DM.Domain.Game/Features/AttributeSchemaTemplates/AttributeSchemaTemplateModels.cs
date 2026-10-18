using System;
using System.Collections.Generic;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.AttributeSchemaTemplates;

/// <summary>
/// A starting character sheet the site offers when a game is created
/// </summary>
/// <remarks>
/// Belongs to the site rather than to whoever typed it in, and a game created
/// from it keeps no link back: the copy is taken by value, so renaming a
/// template, replacing it or hiding it leaves every game alone.
/// </remarks>
public class AttributeSchemaTemplate
{
    /// <summary>
    /// Identifier
    /// </summary>
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

    /// <summary>
    /// Attribute specifications
    /// </summary>
    public IEnumerable<AttributeSpecification> Specifications { get; set; } = [];
}

/// <summary>
/// DTO for creating an attribute schema template
/// </summary>
public class CreateAttributeSchemaTemplate
{
    /// <summary>
    /// Display name
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Attribute specifications. Every identifier is the server's, so the
    /// creation shape is the one that carries none.
    /// </summary>
    public IEnumerable<CreateAttributeSpecification> Specifications { get; set; } = [];
}

/// <summary>
/// DTO for replacing an attribute schema template
/// </summary>
/// <remarks>
/// Replaces every writable field at once. The identifiers of the specifications
/// are handed out anew each time: nothing addresses a specification of a
/// template, because a game holds a copy of its own, so keeping them would buy
/// nothing and would need a rule about which ones survive.
/// </remarks>
public class ReplaceAttributeSchemaTemplate
{
    /// <summary>
    /// Template being replaced
    /// </summary>
    public Guid TemplateId { get; set; }

    /// <summary>
    /// Display name
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Attribute specifications
    /// </summary>
    public IEnumerable<CreateAttributeSpecification> Specifications { get; set; } = [];
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
