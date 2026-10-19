using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Authentication.Credentials;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace DM.Web.API.Middleware;

/// <summary>
/// Middleware for user authentication
/// </summary>
internal class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public AuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext,
        ICredentialsStorage credentialsStorage,
        IWebAuthenticationService authenticationService,
        IIdentityProvider identityProvider,
        IIdentitySetter identitySetter)
    {
        if (identityProvider.Current == null)
        {
            var tokenCredentials = credentialsStorage.ExtractToken(httpContext);
            if (tokenCredentials != null)
            {
                await authenticationService.Authenticate(tokenCredentials, httpContext);
            }
            else
            {
                identitySetter.Current = Identity.Guest();
            }
        }

        // Pushed here, by the request-scoped frame that knows when to pop it.
        var username = identityProvider.Current?.User?.Username;
        using var userProperty = string.IsNullOrEmpty(username) ? null : LogContext.PushProperty("User", username);
        await _next(httpContext);
    }
}
