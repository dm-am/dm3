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
/// Without this every sortBy and sortOrder was a bare <c>"type": "string"</c>,
/// so the contract said nothing about what the endpoint would take, and a
/// reader had to guess the vocabulary from the response fields — which is what
/// API_DESIGN promised would never be necessary ("перечислены они не здесь, а
/// рядом с самим эндпоинтом — в объявлении его параметра").
///
/// The values come from SortVocabulary, the same table the action filter
/// enforces, so the published contract and the answer on the wire are one
/// thing said twice rather than two things kept in step.
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
            "sortBy" => fields,
            "sortOrder" => fields == null ? null : SortVocabulary.SortOrders,
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
    }
}
