using System.Reflection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DM.Web.API.Shared.Authentication;

/// <summary>
/// Swagger filter that documents authentication requirements for API endpoints.
/// BFF Pattern uses HttpOnly cookies for authentication - no header required.
/// </summary>
public class AuthenticationSwaggerFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var requiresAuth = context.MethodInfo.GetCustomAttribute<AuthenticationRequiredAttribute>() != null;

        if (requiresAuth)
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "cookieAuth"
                        }
                    },
                    []
                }
            });
        }
    }
}