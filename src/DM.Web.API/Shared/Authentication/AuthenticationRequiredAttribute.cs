using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DM.Web.API.Shared.Authentication;

/// <summary>
/// Attribute that requires user authentication.
/// Uses IAuthorizationFilter to run BEFORE model binding/validation.
/// </summary>
internal class AuthenticationRequiredAttribute : TypeFilterAttribute
{
    public AuthenticationRequiredAttribute() : base(typeof(AuthenticationRequiredFilter))
    {
    }

    /// <summary>
    /// Refuses a caller who is not signed in.
    /// </summary>
    /// <remarks>
    /// Thrown rather than answered: the body of a refusal has one author,
    /// ErrorHandlingMiddleware.
    /// </remarks>
    internal static void EnsureAuthenticated([NotNull] IIdentity? identity)
    {
        if (identity?.User == null || !identity.User.IsAuthenticated)
        {
            throw new HttpException(HttpStatusCode.Unauthorized, RefusalMessage.AuthenticationRequired);
        }
    }

    /// <summary>
    /// Authorization filter that checks if user is authenticated.
    /// Runs before model binding, so validation errors won't mask auth failures.
    /// </summary>
    private class AuthenticationRequiredFilter(IIdentityProvider identityProvider) : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Nothing reads [AllowAnonymous] for this filter, and the actions a
            // link from a letter opens lift the class-level requirement with it.
            if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            {
                return;
            }

            EnsureAuthenticated(identityProvider.Current);
        }
    }
}
