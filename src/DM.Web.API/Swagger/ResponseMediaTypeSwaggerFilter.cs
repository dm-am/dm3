using System.Collections.Generic;
using System.Linq;
using Microsoft.OpenApi;
using DM.Web.API.Shared.Http;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DM.Web.API.Swagger;

/// <summary>
/// Publishes the media type a response actually carries.
/// </summary>
/// <remarks>
/// Without [Produces] Swashbuckle guesses three types from the formatters, and
/// one formatter is registered while a refusal is application/problem+json from
/// ErrorHandlingMiddleware. A filter, because the answer is a property of the
/// host, not of any one action.
/// </remarks>
internal class ResponseMediaTypeSwaggerFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Responses == null)
        {
            return;
        }

        foreach (var response in operation.Responses)
        {
            if (response.Value is not OpenApiResponse mutable)
            {
                continue;
            }

            var content = mutable.Content;
            if (content == null || content.Count == 0)
            {
                continue;
            }

            var isFailure = int.TryParse(response.Key, out var statusCode) && statusCode >= 400;

            // Swashbuckle emits the same schema under each guessed type.
            var body = content.Values.First();
            mutable.Content = new Dictionary<string, OpenApiMediaType>
            {
                [isFailure ? ApiContentTypes.ProblemJson : ApiContentTypes.Json] = body,
            };
        }
    }
}
