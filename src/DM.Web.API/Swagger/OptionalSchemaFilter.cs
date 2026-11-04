using DM.Domain.Core.Dto;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DM.Web.API.Swagger;

/// <summary>
/// Publishes <see cref="Optional{T}" /> as the value it carries.
/// </summary>
/// <remarks>
/// Its converter reads and writes the bare value, while the generator would
/// publish the C# shape, an object with a "value" property, and a client written
/// against that would get 400.
/// </remarks>
internal class OptionalSchemaFilter : ISchemaFilter
{
    /// <inheritdoc />
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        var type = context.Type;
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Optional<>) ||
            schema is not OpenApiSchema published)
        {
            return;
        }

        var valueType = type.GetGenericArguments()[0];
        var valueSchema = context.SchemaGenerator.GenerateSchema(valueType, context.SchemaRepository);

        if (valueSchema is OpenApiSchema inline)
        {
            // Null is a value the wrapper carries on purpose: it is how a caller
            // clears the field, as against leaving the property out to keep it.
            published.Type = inline.Type | JsonSchemaType.Null;
            published.Format = inline.Format;
            published.AllOf = inline.AllOf;
            published.Enum = inline.Enum;
            published.Items = inline.Items;
        }
        else
        {
            // A value published as a component of its own (an enum) comes back
            // as a reference, and a reference has no fields to copy: the schema
            // points at it instead.
            published.Type = null;
            published.Format = null;
            published.AllOf = [valueSchema];
        }

        published.Properties?.Clear();
    }
}
