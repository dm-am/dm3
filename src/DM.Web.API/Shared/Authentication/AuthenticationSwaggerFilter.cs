using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DM.Web.API.Shared.Authentication;

/// <summary>
/// Swagger filter that documents authentication requirements for API endpoints.
/// BFF Pattern uses HttpOnly cookies for authentication - no header required.
/// </summary>
/// <remarks>
/// Reads both ways an endpoint demands a caller, on the method and on the
/// controller: AuthenticationRequired and RequireRole share no base class.
/// </remarks>
public class AuthenticationSwaggerFilter : IOperationFilter
{
    /// <summary>Name of the security scheme of the session cookie.</summary>
    internal const string CookieScheme = "cookieAuth";

    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (RequiresAuthentication(context))
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(CookieScheme)] = []
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
