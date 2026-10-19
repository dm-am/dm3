using System.Net;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DM.Web.API.Shared.Authentication;

/// <summary>
/// Attribute that requires a minimum user role.
/// Implicitly requires authentication — returns 401 for unauthenticated users, 403 for insufficient role.
/// </summary>
internal class RequireRoleAttribute : TypeFilterAttribute
{
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
            AuthenticationRequiredAttribute.EnsureAuthenticated(identity);

            if (identity.User.Role < minimumRole)
            {
                // The role that would have been enough is not named: the refusal
                // reaches anonymous callers too.
                throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);
            }
        }
    }
}
