using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DM.Web.API.Shared.Authentication;

/// <summary>
/// Attribute that requires a minimum user role.
/// Implicitly requires authentication — returns 401 for unauthenticated users, 403 for insufficient role.
/// </summary>
internal class RequireRoleAttribute : TypeFilterAttribute
{
    /// <inheritdoc />
    public RequireRoleAttribute(UserRole minimumRole) : base(typeof(RequireRoleFilter))
    {
        Arguments = [minimumRole];
    }

    /// <summary>
    /// Authorization filter that checks if user has the required minimum role.
    /// Runs after authentication filter to ensure user is already authenticated.
    /// </summary>
    private class RequireRoleFilter(IIdentityProvider identityProvider, UserRole minimumRole) : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var identity = identityProvider.Current;

            // Return 401 if user is not authenticated
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
                return;
            }

            if (identity.User.Role < minimumRole)
            {
                context.Result = new ObjectResult(new
                {
                    type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                    title = $"Access requires {minimumRole} role or higher",
                    status = StatusCodes.Status403Forbidden
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                    ContentTypes = { "application/problem+json" }
                };
            }
        }
    }
}
