using System.Linq;
using DM.Web.API.Shared.Sorting;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
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
    public void Apply(OpenApiParameter parameter, ParameterFilterContext context)
    {
        // Only query members of a bound query object carry a vocabulary; a
        // route or header parameter of the same name would not.
        var declaringType = context.PropertyInfo?.DeclaringType;
        if (declaringType == null || parameter.Schema?.Type != "string")
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

        parameter.Schema.Enum = values.Select(v => (IOpenApiAny)new OpenApiString(v)).ToList();
    }
}
