using System;
using System.Collections.Generic;
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
    private readonly IAuthenticationService _authenticationService;
    private readonly ISessionService _sessionService;
    private readonly ICredentialsStorage _credentialsStorage;
    private readonly IIdentitySetter _identitySetter;
    private readonly ISuspiciousLoginDetector _suspiciousLoginDetector;
    private readonly ISuspiciousLoginNotificationSender _suspiciousLoginNotificationSender;
    private readonly ISecurityAuditRepository _securityAuditRepository;
    private readonly IEventProducer _eventProducer;
    private readonly ILogger<WebAuthenticationService> _logger;

    public WebAuthenticationService(
        IAuthenticationService authenticationService,
        ISessionService sessionService,
        ICredentialsStorage credentialsStorage,
        IIdentitySetter identitySetter,
        ISuspiciousLoginDetector suspiciousLoginDetector,
        ISuspiciousLoginNotificationSender suspiciousLoginNotificationSender,
        ISecurityAuditRepository securityAuditRepository,
        IEventProducer eventProducer,
        ILogger<WebAuthenticationService> logger)
    {
        _authenticationService = authenticationService;
        _sessionService = sessionService;
        _credentialsStorage = credentialsStorage;
        _identitySetter = identitySetter;
        _suspiciousLoginDetector = suspiciousLoginDetector;
        _suspiciousLoginNotificationSender = suspiciousLoginNotificationSender;
        _securityAuditRepository = securityAuditRepository;
        _eventProducer = eventProducer;
        _logger = logger;
    }

    private async Task<IIdentity> GetAuthenticationResult(AuthCredentials credentials, HttpContext httpContext) => credentials switch
    {
        LoginCredentials loginCredentials => await _authenticationService.Authenticate(
            loginCredentials.Email, loginCredentials.Password, loginCredentials.RememberMe,
            ExtractSessionContext(httpContext),
            // The progressive delay is long enough to outlive a reader who gave up.
            httpContext.RequestAborted),
        TokenCredentials tokenCredentials => await _authenticationService.Authenticate(tokenCredentials.Token),
        SecondFactorCredentials secondFactor => await _authenticationService.CompleteSecondFactor(
            secondFactor.ChallengeId, secondFactor.Code, ExtractSessionContext(httpContext),
            httpContext.RequestAborted),
        UnconditionalCredentials unconditionalCredentials => await _authenticationService.Authenticate(
            unconditionalCredentials.UserId, ExtractSessionContext(httpContext)),
        _ => Identity.Guest()
    };

    private static SessionContext ExtractSessionContext(HttpContext httpContext) => new()
    {
        IpAddress = httpContext.GetClientAddress(),
        UserAgent = httpContext.Request.Headers.UserAgent.ToString()
    };

    /// <inheritdoc />
    public async Task<IIdentity> Authenticate(AuthCredentials credentials, HttpContext httpContext)
    {
        var identity = await Apply(httpContext, await GetAuthenticationResult(credentials, httpContext));

        // The second factor is listed because, for an account with one, it is the
        // step that mints the session.
        if (identity.User.IsAuthenticated &&
            credentials is LoginCredentials or UnconditionalCredentials or SecondFactorCredentials)
        {
            await ReportSuspiciousLogin(identity, httpContext);
        }

        return identity;
    }

    /// <summary>
    /// Reads the login against the trail of the account and, when it stands out,
    /// writes the journal, raises the event on the site and sends the letter.
    /// </summary>
    /// <remarks>
    /// A refusal anywhere in here is logged and swallowed: the login itself is
    /// done, and nothing about the detection is allowed to undo it.
    /// </remarks>
    private async Task ReportSuspiciousLogin(IIdentity identity, HttpContext httpContext)
    {
        var sessionContext = ExtractSessionContext(httpContext);
        try
        {
            var isSuspicious = await _suspiciousLoginDetector.IsSuspiciousAsync(
                identity.User.UserId,
                sessionContext.IpAddress,
                sessionContext.UserAgent);

            if (isSuspicious)
            {
                // The journal first: a record needs no address, unlike the letter.
                await _securityAuditRepository.LogAsync(identity.User.UserId, SecurityEventType.SuspiciousLogin,
                    sessionContext.IpAddress, sessionContext.UserAgent);

                // No try/catch: the producer swallows a refusal and counts it.
                await _eventProducer.SendAsync(
                    EventType.SuspiciousLoginActivity, identity.User.UserId);

                await SendSuspiciousLoginLetter(identity, sessionContext);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for suspicious login");
        }
    }

    /// <summary>
    /// The letter about the login, to an account that has an address: sent
    /// inside the request, and counted when it cannot be.
    /// </summary>
    /// <remarks>
    /// Awaited: MailSender lives in the scope of the request, so a publish the
    /// request does not wait for races its disposal. A refusal is swallowed, the
    /// journal being already written, and counted on the bus counter.
    /// </remarks>
    private async Task SendSuspiciousLoginLetter(IIdentity identity, SessionContext context)
    {
        var email = identity.User.Email;
        if (string.IsNullOrEmpty(email))
        {
            return;
        }

        _logger.LogInformation("Suspicious login detected for user {UserId}", identity.User.UserId);

        try
        {
            await _suspiciousLoginNotificationSender.SendAsync(
                email,
                context.IpAddress,
                context.UserAgent,
                identity.Settings?.TimeZoneId);
        }
        catch (Exception ex)
        {
            MessagingMetrics.PublishFailed.Add(1,
                new KeyValuePair<string, object?>("event", nameof(SecurityEventType.SuspiciousLogin)),
                new KeyValuePair<string, object?>("reason", ex.GetType().Name));

            // The identifier, not the address: the store keeps a month of whatever reaches it.
            _logger.LogWarning(ex,
                "Failed to send suspicious login notification for {UserId}", identity.User.UserId);
        }
    }

    /// <inheritdoc />
    public async Task Logout(HttpContext httpContext) =>
        await Apply(httpContext, await _sessionService.Logout());

    /// <inheritdoc />
    public async Task<IIdentity> LogoutElsewhere(HttpContext httpContext) =>
        await Apply(httpContext, await _sessionService.LogoutElsewhere());

    /// <summary>
    /// Makes the identity the request's own and writes or clears the session cookie for it.
    /// </summary>
    private async Task<IIdentity> Apply(HttpContext httpContext, IIdentity identity)
    {
        _identitySetter.Current = identity;

        if (identity.Error == AuthenticationError.ForgedToken)
        {
            _logger.LogError("Seems like someone is trying to forge the token for {Username}", identity.User.Username);
        }

        await (identity.Error == AuthenticationError.NoError && identity.User.IsAuthenticated
            ? _credentialsStorage.Load(httpContext, identity)
            : _credentialsStorage.Unload(httpContext));

        return identity;
    }
}
