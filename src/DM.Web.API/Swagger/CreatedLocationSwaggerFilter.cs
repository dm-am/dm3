using System.Collections.Generic;
using System.Reflection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DM.Web.API.Swagger;

/// <summary>
/// Publishes the <c>Location</c> header every 201 carries.
/// </summary>
/// <remarks>
/// A filter rather than an attribute per action: setting Location is what
/// CreatedAtRoute does, so it is a property of every 201 in the host. The
/// exceptions carry <see cref="CreatedWithoutLocationAttribute" />, which says why.
/// </remarks>
internal class CreatedLocationSwaggerFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Responses == null ||
            !operation.Responses.TryGetValue("201", out var found) ||
            found is not OpenApiResponse created ||
            context.MethodInfo.GetCustomAttribute<CreatedWithoutLocationAttribute>() != null)
        {
            return;
        }

        created.Headers ??= new Dictionary<string, IOpenApiHeader>();
        created.Headers["Location"] = new OpenApiHeader
        {
            Description = "Address of the created resource.",
            Required = true,
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Format = "uri-reference",
                Example = "/v1/games/3fa85f64-5717-4562-b3fc-2c963f66afa6"
            }
        };
    }
}
