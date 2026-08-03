using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Identity;
using DM.Web.API.Shared.Authentication.Credentials;
using DM.Web.API.Shared.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.Shared.Authentication;

/// <inheritdoc />
internal class WebAuthenticationService : IWebAuthenticationService
{
    private readonly IAuthenticationService authenticationService;
    private readonly ICredentialsStorage credentialsStorage;
    private readonly IIdentitySetter identitySetter;
    private readonly ISuspiciousLoginDetector suspiciousLoginDetector;
    private readonly ISuspiciousLoginNotificationSender suspiciousLoginNotificationSender;
    private readonly ISecurityAuditService securityAuditService;
    private readonly ILogger<WebAuthenticationService> logger;

    /// <inheritdoc />
    public WebAuthenticationService(
        IAuthenticationService authenticationService,
        ICredentialsStorage credentialsStorage,
        IIdentitySetter identitySetter,
        ISuspiciousLoginDetector suspiciousLoginDetector,
        ISuspiciousLoginNotificationSender suspiciousLoginNotificationSender,
        ISecurityAuditService securityAuditService,
        ILogger<WebAuthenticationService> logger)
    {
        this.authenticationService = authenticationService;
        this.credentialsStorage = credentialsStorage;
        this.identitySetter = identitySetter;
        this.suspiciousLoginDetector = suspiciousLoginDetector;
        this.suspiciousLoginNotificationSender = suspiciousLoginNotificationSender;
        this.securityAuditService = securityAuditService;
        this.logger = logger;
    }

    private async Task<IIdentity> GetAuthenticationResult(AuthCredentials credentials, HttpContext? httpContext) => credentials switch
    {
        LoginCredentials loginCredentials => await authenticationService.Authenticate(
            loginCredentials.Email, loginCredentials.Password, loginCredentials.RememberMe,
            ExtractSessionContext(httpContext)),
        TokenCredentials tokenCredentials => await authenticationService.Authenticate(tokenCredentials.Token),
        UnconditionalCredentials unconditionalCredentials => await authenticationService.Authenticate(
            unconditionalCredentials.UserId),
        _ => Identity.Guest()
    };

    private static SessionContext? ExtractSessionContext(HttpContext? httpContext)
    {
        if (httpContext == null)
            return null;

        return new SessionContext
        {
            IpAddress = httpContext.GetClientAddress(),
            UserAgent = httpContext.Request.Headers.UserAgent.ToString()
        };
    }

    /// <inheritdoc />
    public async Task<IIdentity> Authenticate(AuthCredentials credentials, HttpContext httpContext)
    {
        var identity = identitySetter.Current = await GetAuthenticationResult(credentials, httpContext);
        await TryLoadAuthenticationResult(httpContext, identity);

        // Suspicious login detection for successful logins
        if (identity.User.IsAuthenticated && credentials is LoginCredentials)
        {
            var sessionContext = ExtractSessionContext(httpContext);
            try
            {
                var isSuspicious = await suspiciousLoginDetector.IsSuspiciousAsync(
                    identity.User.UserId,
                    sessionContext?.IpAddress,
                    sessionContext?.UserAgent);

                // The journal first, and without the address check below it: a
                // letter needs somewhere to go, a record does not, and the record
                // is what the owner of the account reads afterwards.
                if (isSuspicious)
                {
                    await securityAuditService.LogAsync(identity.User.UserId, SecurityEventType.SuspiciousLogin,
                        sessionContext?.IpAddress, sessionContext?.UserAgent);
                }

                if (isSuspicious && !string.IsNullOrEmpty(identity.User.Email))
                {
                    logger.LogInformation(
                        "Suspicious login detected for user {UserId} from IP {IpAddress}",
                        identity.User.UserId, sessionContext?.IpAddress);

                    // Fire and forget - don't block login
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await suspiciousLoginNotificationSender.SendAsync(
                                identity.User.Email,
                                identity.User.Username,
                                sessionContext?.IpAddress,
                                sessionContext?.UserAgent);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to send suspicious login notification");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to check for suspicious login");
            }
        }

        return identity;
    }

    /// <inheritdoc />
    public async Task Logout(HttpContext httpContext)
    {
        var identity = identitySetter.Current = await authenticationService.Logout();
        await TryLoadAuthenticationResult(httpContext, identity);
    }

    /// <inheritdoc />
    public async Task<IIdentity> LogoutElsewhere(HttpContext httpContext)
    {
        var identity = identitySetter.Current = await authenticationService.LogoutElsewhere();
        await TryLoadAuthenticationResult(httpContext, identity);
        return identity;
    }

    private Task TryLoadAuthenticationResult(HttpContext httpContext, IIdentity identity)
    {
        if (identity.Error == AuthenticationError.ForgedToken)
        {
            logger.LogError("Seems like someone is trying to forge the token for {Username}", identity.User.Username);
        }

        return identity.Error == AuthenticationError.NoError && identity.User.IsAuthenticated
            ? credentialsStorage.Load(httpContext, identity)
            : credentialsStorage.Unload(httpContext);
    }
}