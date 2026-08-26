using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Authentication.Credentials;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace DM.Web.API.Middleware;

/// <summary>
/// Middleware for user authentication
/// </summary>
public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    /// <inheritdoc />
    public AuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Before request
    /// </summary>
    public async Task InvokeAsync(HttpContext httpContext,
        ICredentialsStorage credentialsStorage,
        IWebAuthenticationService authenticationService,
        IIdentityProvider identityProvider,
        IIdentitySetter identitySetter)
    {
        if (identityProvider.Current == null)
        {
            var tokenCredentials = await credentialsStorage.ExtractToken(httpContext);
            if (tokenCredentials != null)
            {
                await authenticationService.Authenticate(tokenCredentials, httpContext);
            }
            else
            {
                // Set guest identity for unauthenticated requests
                identitySetter.Current = DM.Domain.Account.Features.Authentication.Identity.Guest();
            }
        }

        // Tagging log events with the caller belongs to the host, not to the
        // identity store: PushProperty returns the disposable that pops the
        // enricher back off, and only a request-scoped frame knows when that
        // should happen. The identity storage used to push from its own setter
        // and drop the disposable, so every assignment left a frame behind.
        var username = identityProvider.Current?.User?.Username;
        if (string.IsNullOrEmpty(username))
        {
            await _next(httpContext);
            return;
        }

        using (LogContext.PushProperty("User", username))
        {
            await _next(httpContext);
        }
    }
}
