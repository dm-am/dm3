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

            // Both refusals are thrown for the middleware to shape, for the reason
            // AuthenticationRequiredAttribute states, and both take their wording
            // from the dictionary: this 401 is the same event as the one that
            // filter refuses, and this 403 is the same event as an intention
            // refusal, so a second wording for either would only drift.
            if (identity?.User == null || !identity.User.IsAuthenticated)
            {
                throw new HttpException(HttpStatusCode.Unauthorized, RefusalMessage.AuthenticationRequired);
            }

            if (identity.User.Role < minimumRole)
            {
                // The one case where a refusal by rank names its reason: the
                // rank on the account is enough and was withheld for want of a
                // second factor. The generic sentence is not merely unhelpful
                // here, it is untrue - the caller is an administrator being told
                // the page is for administrators - and the reader would go
                // looking for a broken site rather than for his own settings.
                //
                // Guarded by the recorded rank so that it stays the reason and
                // not a hint: a withheld senior moderator asking for an
                // administrator's page is short of the rank either way, and gets
                // the refusal that names nothing.
                if (identity.User.PrivilegeWithheld && identity.User.RecordedRole >= minimumRole)
                {
                    throw new HttpException(
                        HttpStatusCode.Forbidden, RefusalMessage.PrivilegeWithheldWithoutTwoFactor);
                }

                // The role that would have been enough is not named. The reader
                // cannot grant it to themselves, and the refusal is shown to an
                // anonymous caller too.
                throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccessDenied);
            }
        }
    }
}
