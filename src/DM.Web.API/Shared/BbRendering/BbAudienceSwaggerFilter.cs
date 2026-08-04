using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DM.Infrastructure.Core.Parsing;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DM.Web.API.Shared.BbRendering;

/// <summary>
/// Swagger operation filter documenting the <c>X-Dm-Audience</c> header
/// that controls how BBCode-bearing response fields are rendered.
/// </summary>
/// <remarks>
/// Declared where it changes something and nowhere else. On all 363 operations
/// it told the reader nothing: a generated client grew an unused argument on
/// every method, and the one question the header answers — does this response
/// depend on it, and may a shared cache therefore store it without a Vary —
/// could not be read off the contract at all.
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
