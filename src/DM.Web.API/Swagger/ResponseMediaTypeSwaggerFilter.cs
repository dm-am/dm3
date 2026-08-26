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
/// With no [Produces] anywhere in the host, Swashbuckle guesses from the
/// registered formatters and writes text/plain, application/json and text/json
/// under every body. Two of the three never travel — one output formatter is
/// registered — and under a refusal none of them does: ErrorHandlingMiddleware
/// answers with application/problem+json. A client generated from the document
/// picks its deserialiser by media type, so naming three wrong ones is worse
/// than naming none.
///
/// A filter rather than an attribute per controller: [Produces] is a rule every
/// new controller has to remember, and the answer here is a property of the host
/// rather than of any one action. Nothing on the wire changes — this describes
/// what the middleware and the formatter already do.
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

            // Swashbuckle emits the same schema under each guessed type, so any
            // entry is the body; what changes is the single name it is published
            // under.
            var body = content.Values.First();
            mutable.Content = new Dictionary<string, OpenApiMediaType>
            {
                // ApiContentTypes.ProblemJson is what ErrorHandlingMiddleware writes on
                // every refusal; documenting anything else here would describe a
                // response the API does not send.
                [isFailure ? ApiContentTypes.ProblemJson : ApiContentTypes.Json] = body,
            };
        }
    }
}
