using DM.Services.Authentication.Implementation.UserIdentity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DM.Web.API.Authentication;

/// <summary>
/// Attribute that requires user authentication.
/// Uses IAuthorizationFilter to run BEFORE model binding/validation.
/// </summary>
internal class AuthenticationRequiredAttribute : TypeFilterAttribute
{
    /// <inheritdoc />
    public AuthenticationRequiredAttribute() : base(typeof(AuthenticationRequiredFilter))
    {
    }

    /// <summary>
    /// Authorization filter that checks if user is authenticated.
    /// Runs before model binding, so validation errors won't mask auth failures.
    /// </summary>
    private class AuthenticationRequiredFilter(IIdentityProvider identityProvider) : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Check if identity is set and user is authenticated
            var identity = identityProvider.Current;
            if (identity?.User == null || !identity.User.IsAuthenticated)
            {
                context.Result = new ObjectResult(new
                {
                    type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                    title = "User must be authenticated",
                    status = StatusCodes.Status401Unauthorized
                })
                {
                    StatusCode = StatusCodes.Status401Unauthorized,
                    ContentTypes = { "application/problem+json" }
                };
            }
        }
    }
}