using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Core.Tracing;
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
    private readonly ISecurityAuditRepository securityAuditRepository;
    private readonly IEventProducer eventProducer;
    private readonly ILogger<WebAuthenticationService> logger;

    /// <inheritdoc />
    public WebAuthenticationService(
        IAuthenticationService authenticationService,
        ICredentialsStorage credentialsStorage,
        IIdentitySetter identitySetter,
        ISuspiciousLoginDetector suspiciousLoginDetector,
        ISuspiciousLoginNotificationSender suspiciousLoginNotificationSender,
        ISecurityAuditRepository securityAuditRepository,
        IEventProducer eventProducer,
        ILogger<WebAuthenticationService> logger)
    {
        this.authenticationService = authenticationService;
        this.credentialsStorage = credentialsStorage;
        this.identitySetter = identitySetter;
        this.suspiciousLoginDetector = suspiciousLoginDetector;
        this.suspiciousLoginNotificationSender = suspiciousLoginNotificationSender;
        this.securityAuditRepository = securityAuditRepository;
        this.eventProducer = eventProducer;
        this.logger = logger;
    }

    private async Task<IIdentity> GetAuthenticationResult(AuthCredentials credentials, HttpContext? httpContext) => credentials switch
    {
        LoginCredentials loginCredentials => await authenticationService.Authenticate(
            loginCredentials.Email, loginCredentials.Password, loginCredentials.RememberMe,
            ExtractSessionContext(httpContext),
            // The progressive delay of this path is long enough that a reader who
            // gave up leaves a request asleep behind them, holding its connection.
            httpContext?.RequestAborted ?? CancellationToken.None),
        TokenCredentials tokenCredentials => await authenticationService.Authenticate(tokenCredentials.Token),
        UnconditionalCredentials unconditionalCredentials => await authenticationService.Authenticate(
            unconditionalCredentials.UserId, ExtractSessionContext(httpContext)),
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
        if (identity.User.IsAuthenticated && credentials is LoginCredentials or UnconditionalCredentials)
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
                    await securityAuditRepository.LogAsync(identity.User.UserId, SecurityEventType.SuspiciousLogin,
                        sessionContext?.IpAddress, sessionContext?.UserAgent);

                    // The notification on the site, beside the letter below. The
                    // event type, its wording and its category all existed and
                    // nothing ever produced it, so the Security category the
                    // settings screen offers was one item short of what it promised.
                    // No try/catch here: the producer swallows a refusal and counts
                    // it, which is the trade SYSTEM.md states for every event.
                    await eventProducer.SendAsync(
                        EventType.SuspiciousLoginActivity, identity.User.UserId);
                }

                if (isSuspicious && !string.IsNullOrEmpty(identity.User.Email))
                {
                    logger.LogInformation(
                        "Suspicious login detected for user {UserId} from IP {IpAddress}",
                        identity.User.UserId, sessionContext?.IpAddress);

                    // Awaited inside the request, and it has to be. Sending this
                    // publishes to dm.mail.sending through MailSender, which lives
                    // in the scope of the request and gives its rented channel back
                    // in Dispose - so the publish is only legal while that scope is
                    // alive. Handed to a task the request does not wait for, it
                    // raced the end of the request with nothing ordering the two,
                    // and the losing side published through a disposed sender.
                    //
                    // The login must not fail over an informational letter, so a
                    // refusal is swallowed - the security journal above is the
                    // record that matters and it is already written. Swallowing is
                    // only allowed while it is counted, and the counter is the one
                    // the event bus uses for the same trade.
                    //
                    // What must not be done here: no timeout through Task.WhenAny
                    // and no second Task.Run. Both put an abandoned task back on
                    // top of a disposable owned by someone else's scope, which is
                    // the original defect. A ceiling on publishing, if one is ever
                    // needed, belongs to the connection factory and applies to all
                    // nine letters of the product at once.
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
                        MessagingMetrics.PublishFailed.Add(1,
                            new KeyValuePair<string, object?>("event", nameof(SecurityEventType.SuspiciousLogin)),
                            new KeyValuePair<string, object?>("reason", ex.GetType().Name));

                        // The identifier, not the address: the address names a
                        // person, and the store keeps a month of whatever is
                        // written to it.
                        logger.LogWarning(ex,
                            "Failed to send suspicious login notification for {UserId}", identity.User.UserId);
                    }
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
