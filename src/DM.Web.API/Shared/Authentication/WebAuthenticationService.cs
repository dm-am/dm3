using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Web.API.Shared.Authentication.Credentials;
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
    private readonly ILogger<WebAuthenticationService> logger;

    /// <inheritdoc />
    public WebAuthenticationService(
        IAuthenticationService authenticationService,
        ICredentialsStorage credentialsStorage,
        IIdentitySetter identitySetter,
        ISuspiciousLoginDetector suspiciousLoginDetector,
        ISuspiciousLoginNotificationSender suspiciousLoginNotificationSender,
        ILogger<WebAuthenticationService> logger)
    {
        this.authenticationService = authenticationService;
        this.credentialsStorage = credentialsStorage;
        this.identitySetter = identitySetter;
        this.suspiciousLoginDetector = suspiciousLoginDetector;
        this.suspiciousLoginNotificationSender = suspiciousLoginNotificationSender;
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
            IpAddress = ExtractClientIp(httpContext),
            UserAgent = httpContext.Request.Headers.UserAgent.ToString()
        };
    }

    private static string ExtractClientIp(HttpContext httpContext)
    {
        // X-Forwarded-For for reverse proxy (nginx, cloudflare)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP (client), the rest are proxies
            var clientIp = forwardedFor.Split(',', System.StringSplitOptions.TrimEntries).First();
            if (IPAddress.TryParse(clientIp, out _))
                return clientIp;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
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