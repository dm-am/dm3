using System.Collections.Generic;
using System.Reflection;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DM.Web.API.Swagger;

/// <summary>
/// Publishes the <c>Location</c> header every 201 carries.
/// </summary>
/// <remarks>
/// Sixty-two operations declared a 201 and not one of them declared a header,
/// so the address of the thing that had just been created was on the wire and
/// absent from the contract: a consumer written against the document had no
/// choice but to re-query the collection it had just posted to.
///
/// A filter rather than an attribute per action, for the same reason as the
/// media type next door: setting Location is what CreatedAtRoute does, so it is
/// a property of every 201 in the host and not a thing each new action has to
/// remember to describe. An operation that answers 201 without a Location is a
/// defect of the action, not of the document — that is the case the API_DESIGN
/// rule covers by saying such a response should not be a 201 at all.
///
/// Eighteen actions are in exactly that case today: they answer through
/// StatusCode(201, ...) and set no header. Declaring the header over them turned
/// one lie into another — the document promised an address that never arrives,
/// which is worse than the silence it replaced, because a consumer can see
/// silence. They carry <see cref="CreatedWithoutLocationAttribute" />, this
/// filter skips them, and CreatedLocationShould keeps the marker attached to
/// exactly those actions.
/// </remarks>
internal class CreatedLocationSwaggerFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!operation.Responses.TryGetValue("201", out var created) ||
            context.MethodInfo.GetCustomAttribute<CreatedWithoutLocationAttribute>() != null)
        {
            return;
        }

        created.Headers ??= new Dictionary<string, OpenApiHeader>();
        created.Headers["Location"] = new OpenApiHeader
        {
            Description = "Address of the created resource.",
            Required = true,
            Schema = new OpenApiSchema
            {
                Type = "string",
                Format = "uri-reference",
                Example = new OpenApiString("/v1/games/3fa85f64-5717-4562-b3fc-2c963f66afa6")
            }
        };
    }
}
