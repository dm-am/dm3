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
            // [AllowAnonymous] is metadata, and this filter is not the framework's
            // authorization middleware, so nothing reads it on our behalf. The
            // attribute sits on the class here and is lifted off single actions —
            // confirming an email change, finishing a username change — which are
            // the actions a person reaches by following a link from their mailbox,
            // often in a browser where they are not signed in. Those answered 401
            // and the link looked broken.
            if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            {
                return;
            }

            // Thrown rather than answered with a result of this filter's own.
            // The body of a refusal has one author, ErrorHandlingMiddleware, and
            // the hand-built one here carried neither the correlation token every
            // other refusal carries nor a sentence in the language of the screen
            // it is shown on.
            var identity = identityProvider.Current;
            if (identity?.User == null || !identity.User.IsAuthenticated)
            {
                throw new HttpException(HttpStatusCode.Unauthorized, RefusalMessage.AuthenticationRequired);
            }
        }
    }
}
