using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Game.AttributeSchemas;

/// <summary>
/// Partial update of an attribute schema
/// </summary>
/// <remarks>
/// Every field is optional: an omitted one keeps its current value. The read
/// model cannot be reused here — its <c>Type</c> is a plain enum whose default
/// is <see cref="SchemaType.Public"/>, so an omitted type would publish a
/// private schema, and its <c>Specifications</c> default to an empty list,
/// which the repository would store as "all specifications removed".
/// </remarks>
public class UpdateAttributeSchemaRequest
{
    /// <summary>
    /// Schema title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Schema type
    /// </summary>
    public SchemaType? Type { get; set; }

    /// <summary>
    /// Schema specifications. Replaces the whole set when present.
    /// </summary>
    public IEnumerable<AttributeSpecification>? Specifications { get; set; }
}
