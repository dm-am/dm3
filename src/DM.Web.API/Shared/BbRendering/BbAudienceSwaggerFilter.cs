using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DM.Infrastructure.Core.Parsing;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DM.Web.API.Shared.BbRendering;

/// <summary>
/// Swagger operation filter documenting the <c>X-Dm-Audience</c> header
/// that controls how BBCode-bearing response fields are rendered.
/// </summary>
/// <remarks>
/// Declared only where it changes something, so the contract says which responses
/// depend on it and a shared cache may store the rest without a Vary.
/// </remarks>
internal class BbAudienceSwaggerFilter : IOperationFilter
{
    /// <summary>
    /// Whether a response type carries server-rendered BBCode anywhere in its
    /// graph. Cycles are broken by the visited set, and a type already ruled out
    /// on another branch stays ruled out.
    /// </summary>
    private static bool CarriesBbText(Type type, HashSet<Type> visited)
    {
        if (type == typeof(string) || type.IsPrimitive || type.IsEnum || !visited.Add(type))
        {
            return false;
        }

        if (typeof(BbText).IsAssignableFrom(type))
        {
            return true;
        }

        if (type.IsArray && CarriesBbText(type.GetElementType()!, visited))
        {
            return true;
        }

        if (type.IsGenericType && type.GetGenericArguments().Any(a => CarriesBbText(a, visited)))
        {
            return true;
        }

        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(p => CarriesBbText(p.PropertyType, visited));
    }

    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var rendersBbCode = context.ApiDescription.SupportedResponseTypes
            .Select(r => r.Type)
            .Any(t => t != null && CarriesBbText(t, new HashSet<Type>()));

        if (!rendersBbCode)
        {
            return;
        }

        operation.Parameters ??= new List<IOpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = BbAudienceHeader.HeaderName,
            In = ParameterLocation.Header,
            Required = false,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            Description =
                "Rendering audience for BbText-bearing response fields. One of: " +
                $"{BbAudienceHeader.Serialize(RenderAudience.Display)} (default, permission-filtered view), " +
                $"{BbAudienceHeader.Serialize(RenderAudience.AuthorEdit)} (author loading own content for editing), " +
                $"{BbAudienceHeader.Serialize(RenderAudience.PlainText)} (email / notification body), " +
                $"{BbAudienceHeader.Serialize(RenderAudience.EmbedSafe)} (link preview / cross-post embed)."
        });
    }
}
