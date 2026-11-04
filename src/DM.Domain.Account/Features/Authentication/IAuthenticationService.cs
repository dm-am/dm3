using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;

namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// User authentication service
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticate via email credentials
    /// </summary>
    /// <param name="email">User email address</param>
    /// <param name="password">User password</param>
    /// <param name="rememberMe">If true, the session is persistent and lives for the configured
    /// persistent lifetime; otherwise for the ordinary one</param>
    /// <param name="context">Session context with device info</param>
    /// <param name="cancellationToken">Cancellation token of the request being served</param>
    /// <returns>Authentication identity</returns>
    Task<IIdentity> Authenticate(string email, string password, bool rememberMe = true,
        SessionContext? context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finish a login whose password step issued a challenge.
    /// </summary>
    /// <remarks>
    /// The state of the account is checked again here rather than carried over
    /// from the first step: a ban issued in the five minutes somebody spent
    /// looking for their phone has to take effect.
    ///
    /// Every way of failing answers the same way, so that nothing tells the
    /// caller which of the walls they are standing at: an unknown challenge, an
    /// expired one, one out of attempts, a wrong code and a spent recovery code
    /// are one refusal.
    /// </remarks>
    /// <param name="challengeId">Challenge from the cookie</param>
    /// <param name="code">Code from the device, or a recovery code</param>
    /// <param name="context">Address and agent the second step came from</param>
    /// <param name="cancellationToken">Cancellation token of the request being served</param>
    /// <returns>Authentication identity</returns>
    Task<IIdentity> CompleteSecondFactor(
        Guid challengeId, string code, SessionContext? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticate via token credentials
    /// </summary>
    /// <param name="authToken">Authentication token</param>
    /// <returns>Authentication identity</returns>
    Task<IIdentity> Authenticate(string authToken);

    /// <summary>
    /// Authenticate unconditionally
    /// </summary>
    /// <remarks>
    /// A session without a password, and an ordinary one otherwise: it is written
    /// into the security journal and the device list like any login, because the
    /// person whose account it is has to be able to see it.
    /// </remarks>
    /// <param name="userId">User identifier</param>
    /// <param name="context">Address and agent the session is opened from</param>
    /// <returns>Authentication identity</returns>
    Task<IIdentity> Authenticate(Guid userId, SessionContext? context = null);
}
