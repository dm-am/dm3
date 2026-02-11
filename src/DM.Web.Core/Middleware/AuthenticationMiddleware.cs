using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Web.Core.Authentication;
using DM.Web.Core.Authentication.Credentials;
using Microsoft.AspNetCore.Http;

namespace DM.Web.Core.Middleware;

/// <summary>
/// Middleware for user authentication
/// </summary>
public class AuthenticationMiddleware
{
    private readonly RequestDelegate next;

    /// <inheritdoc />
    public AuthenticationMiddleware(RequestDelegate next)
    {
        this.next = next;
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
                identitySetter.Current = DM.Services.Authentication.Dto.Identity.Guest();
            }
        }

        await next(httpContext);
    }
}