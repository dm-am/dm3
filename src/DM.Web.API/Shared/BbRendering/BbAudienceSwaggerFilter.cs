using System.Collections.Generic;
using DM.Infrastructure.Core.Parsing;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DM.Web.API.Shared.BbRendering;

/// <summary>
/// Swagger operation filter documenting the <c>X-Dm-Audience</c> header
/// that controls how BBCode-bearing response fields are rendered.
/// </summary>
internal class BbAudienceSwaggerFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        (operation.Parameters ?? (operation.Parameters = new List<OpenApiParameter>()))
            .Add(new OpenApiParameter
            {
                Name = BbAudienceHeader.HeaderName,
                In = ParameterLocation.Header,
                Required = false,
                Schema = new OpenApiSchema { Type = "string" },
                Description =
                    "Rendering audience for BbText-bearing response fields. One of: " +
                    "display (default, permission-filtered view), " +
                    "author_edit (author loading own content for editing), " +
                    "plain_text (email / notification body), " +
                    "embed_safe (link preview / cross-post embed)."
            });
    }
}
