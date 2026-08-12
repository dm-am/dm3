using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DM.Web.API.Shared.Authentication;

/// <summary>
/// Swagger filter that documents authentication requirements for API endpoints.
/// BFF Pattern uses HttpOnly cookies for authentication - no header required.
/// </summary>
/// <remarks>
/// Asked of the method alone, this saw neither of the two ways an endpoint
/// actually demands a caller: the attribute written once on the controller, and
/// RequireRole, which is a TypeFilterAttribute and shares no base class with
/// AuthenticationRequired even though it refuses anonymous callers with the same
/// 401. A hundred and three operations documented that 401 with no security
/// scheme beside it, so a generated client had nothing to send.
/// </remarks>
public class AuthenticationSwaggerFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (RequiresAuthentication(context))
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

    /// <summary>
    /// Whether reaching this operation takes a signed-in caller.
    /// </summary>
    /// <remarks>
    /// AllowAnonymous wins over both, on either level, because that is how the
    /// pipeline itself resolves them: the authentication filter steps aside for
    /// endpoint metadata that says the door is open.
    /// </remarks>
    private static bool RequiresAuthentication(OperationFilterContext context)
    {
        if (context.ApiDescription.CustomAttributes().OfType<IAllowAnonymous>().Any())
        {
            return false;
        }

        return Declared<AuthenticationRequiredAttribute>(context) || Declared<RequireRoleAttribute>(context);
    }

    private static bool Declared<TAttribute>(OperationFilterContext context) where TAttribute : Attribute =>
        context.MethodInfo.GetCustomAttribute<TAttribute>() != null ||
        context.MethodInfo.DeclaringType?.GetCustomAttribute<TAttribute>(inherit: true) != null;
}
