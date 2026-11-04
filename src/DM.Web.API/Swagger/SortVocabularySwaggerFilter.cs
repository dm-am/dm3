using System;
using System.Linq;
using System.Text.Json.Nodes;
using DM.Web.API.Shared.Sorting;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DM.Web.API.Swagger;

/// <summary>
/// Publishes the sort fields an endpoint accepts as the parameter's enum.
/// </summary>
/// <remarks>
/// The values come from SortVocabulary, the table the action filter enforces,
/// so the contract and the answer on the wire are one thing.
/// </remarks>
internal class SortVocabularySwaggerFilter : IParameterFilter
{
    /// <inheritdoc />
    public void Apply(IOpenApiParameter parameter, ParameterFilterContext context)
    {
        // Only query members of a bound query object carry a vocabulary; a
        // route or header parameter of the same name would not.
        var declaringType = context.PropertyInfo?.DeclaringType;
        if (declaringType == null || parameter.Schema is not OpenApiSchema schema ||
            schema.Type != JsonSchemaType.String)
        {
            return;
        }

        var fields = SortVocabulary.FieldsOf(declaringType);
        var values = parameter.Name switch
        {
            _ when Names(SortVocabulary.SortByProperty) => fields,
            _ when Names(SortVocabulary.SortOrderProperty) => fields == null ? null : SortVocabulary.SortOrders,
            _ => null
        };

        // An empty vocabulary belongs to a type whose sort field is an enum of
        // its own: publishing "enum": [] there would say the parameter takes
        // nothing at all.
        if (values == null || values.Count == 0)
        {
            return;
        }

        schema.Enum = values.Select(v => (JsonNode)v!).ToList();

        // The parameter is published in camel case, the property is named in Pascal case.
        bool Names(string property) => string.Equals(parameter.Name, property, StringComparison.OrdinalIgnoreCase);
    }
}
