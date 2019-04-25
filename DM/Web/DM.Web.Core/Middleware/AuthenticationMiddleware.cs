using System.Threading.Tasks;
using DM.Web.Core.Authentication;
using DM.Web.Core.Authentication.Credentials;
using Microsoft.AspNetCore.Http;

namespace DM.Web.Core.Middleware
{
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
        /// <param name="httpContext"></param>
        /// <param name="credentialsStorage"></param>
        /// <param name="authenticationService"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext httpContext,
            ICredentialsStorage credentialsStorage,
            IWebAuthenticationService authenticationService)
        {
            var tokenCredentials = await credentialsStorage.ExtractToken(httpContext);
            await authenticationService.Authenticate(tokenCredentials, httpContext);
            await next(httpContext);
        }
    }
}