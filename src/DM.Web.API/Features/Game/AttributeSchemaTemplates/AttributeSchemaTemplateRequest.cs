using System.Collections.Generic;
using DM.Web.API.Features.Game.AttributeSchemas;

namespace DM.Web.API.Features.Game.AttributeSchemaTemplates;

/// <summary>
/// The whole writable part of a template: what a moderator may put in it
/// </summary>
/// <remarks>
/// Its own type rather than the response model, which also carries the
/// identifier the server mints and the default flag the server moves. One body
/// for creation and for replacement, because both write every field: a template
/// that could not be created cannot be arrived at by editing one either.
/// </remarks>
public class AttributeSchemaTemplateRequest
{
    /// <summary>
    /// Display name
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Attribute specifications. Every identifier is handed out by the server.
    /// </summary>
    public IEnumerable<AttributeSpecification> Specifications { get; set; } = [];
}
