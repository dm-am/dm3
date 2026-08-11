using System;
using System.Linq;
using DM.Domain.Core.Dto;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DM.Web.API.Swagger;

/// <summary>
/// Publishes <see cref="Optional{T}" /> as the value it carries.
/// </summary>
/// <remarks>
/// The wrapper exists to tell "property absent" from "property set to null" in a
/// PATCH, and its converter reads and writes the bare value. The generator knew
/// none of that and published the C# shape instead — an object with a read-only
/// "value" property — so a client written against the document sent
/// {"previousRoomId": {"value": "..."}}, the converter met an object where it
/// expected a GUID, and the request came back 400. The same schema is published
/// on responses, where it describes something the server never sends.
/// </remarks>
internal class OptionalSchemaFilter : ISchemaFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var type = context.Type;
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Optional<>))
        {
            return;
        }

        var valueType = type.GetGenericArguments()[0];
        var valueSchema = context.SchemaGenerator.GenerateSchema(valueType, context.SchemaRepository);

        schema.Type = valueSchema.Type;
        schema.Format = valueSchema.Format;
        schema.Reference = valueSchema.Reference;
        schema.AllOf = valueSchema.AllOf;
        schema.Enum = valueSchema.Enum;
        schema.Items = valueSchema.Items;
        schema.Properties.Clear();

        // Null is a value the wrapper carries on purpose: it is how a caller
        // clears the field, as against leaving the property out to keep it.
        schema.Nullable = true;
    }
}
