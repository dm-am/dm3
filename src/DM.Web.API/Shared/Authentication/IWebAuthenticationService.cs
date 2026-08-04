using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Web.API.Shared.Authentication.Credentials;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.Shared.Authentication;

/// <summary>
/// Authentication service
/// </summary>
public interface IWebAuthenticationService
{
    /// <summary>
    /// Authenticate
    /// </summary>
    /// <param name="credentials">Credentials</param>
    /// <param name="httpContext">HTTP context</param>
    /// <returns>Authenticated user identity</returns>
    Task<IIdentity> Authenticate(AuthCredentials credentials, HttpContext httpContext);

    /// <summary>
    /// Logout as current user
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    Task Logout(HttpContext httpContext);

    /// <summary>
    /// Logout as current user from every device
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    /// <returns>Updated user identity</returns>
    Task<IIdentity> LogoutElsewhere(HttpContext httpContext);
}
