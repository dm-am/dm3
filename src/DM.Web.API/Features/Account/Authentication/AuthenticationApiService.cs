using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;
using DM.Infrastructure.Core.Tracing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Personal.Preferences;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Authentication.Credentials;
using DM.Web.API.Shared.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Web.API.Features.Account.Authentication;

/// <summary>
/// API service for user authentication and session management
/// </summary>
public class AuthenticationApiService
{
    /// <summary>The value of a marker key beside the sentence of a refusal: the client reads the key alone.</summary>
    private const string Marker = "true";

    private readonly IWebAuthenticationService _authenticationService;
    private readonly ISessionService _sessionService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserService _userService;
    private readonly ILoginRecordService _loginRecordService;
    private readonly UserMapper _mapper;
    private readonly PreferencesMapper _preferencesMapper;
    private readonly TwoFactorChallengeCookie _challengeCookie;
    private readonly ILogger<AuthenticationApiService> _logger;
    private readonly TwoFactorConfiguration _twoFactorConfig;

    public AuthenticationApiService(
        IWebAuthenticationService authenticationService,
        ISessionService sessionService,
        IIdentityProvider identityProvider,
        IUserService userService,
        ILoginRecordService loginRecordService,
        UserMapper mapper,
        PreferencesMapper preferencesMapper,
        TwoFactorChallengeCookie challengeCookie,
        ILogger<AuthenticationApiService> logger,
        IOptions<TwoFactorConfiguration> twoFactorConfig)
    {
        _authenticationService = authenticationService;
        _sessionService = sessionService;
        _identityProvider = identityProvider;
        _userService = userService;
        _loginRecordService = loginRecordService;
        _mapper = mapper;
        _preferencesMapper = preferencesMapper;
        _challengeCookie = challengeCookie;
        _logger = logger;
        _twoFactorConfig = twoFactorConfig.Value;
    }

    /// <summary>
    /// Login using email-password credentials
    /// </summary>
    /// <param name="request">Email and password</param>
    /// <param name="httpContext">HTTP context</param>
    /// <returns>Login response with user and preferences</returns>
    public async Task<LoginResponse> Login(LoginRequest request, HttpContext httpContext)
    {
        var credentials = new LoginCredentials
        {
            Email = request.Email,
            Password = request.Password,
            RememberMe = request.RememberMe
        };

        var identity = await _authenticationService.Authenticate(credentials, httpContext);

        // The password was right and the login is not over: only the stage travels
        // back, the challenge in a cookie of its own, and the step that finishes
        // the login records it.
        if (identity.TwoFactorChallengeId.HasValue)
        {
            await _challengeCookie.Write(httpContext, identity.TwoFactorChallengeId.Value,
                TimeSpan.FromMinutes(_twoFactorConfig.ChallengeLifetimeMinutes));
            return new LoginResponse { TwoFactorRequired = true };
        }

        await RecordLoginAttempt(request.Email, httpContext,
            isSuccessful: identity.Error == AuthenticationError.NoError);

        if (identity.Error == AuthenticationError.NoError)
        {
            return await SignedInResponse();
        }

        // Counted here and not per branch, so no refusal drops out of the count.
        CountFailure(identity);
        throw LoginRefusal(identity, httpContext);
    }

    /// <summary>
    /// The answer a finished login comes back with, from the password step and
    /// from the second factor alike: the user and the preferences of the
    /// identity the step has just set.
    /// </summary>
    private async Task<LoginResponse> SignedInResponse()
    {
        var userDetails = await _userService.GetDetailsAsync(_identityProvider.Current.User.Username);
        return new LoginResponse
        {
            User = _mapper.ToUser(userDetails),
            Preferences = _preferencesMapper.ToPreferences(userDetails.Settings ?? UserSettings.Default)
        };
    }

    /// <summary>
    /// The refusal the password step comes back with, one per reason.
    /// </summary>
    /// <remarks>
    /// Returned rather than thrown, so that the caller counts the refusal before
    /// it leaves and this switch stays about the answers only.
    /// </remarks>
    private Exception LoginRefusal(IIdentity identity, HttpContext httpContext)
    {
        switch (identity.Error)
        {
            case AuthenticationError.WrongLogin:
            case AuthenticationError.WrongPassword:
                // Unified error message to prevent user enumeration attacks
                return new HttpBadRequestException(new Dictionary<string, string>
                {
                    ["password"] = RefusalMessage.WrongCredentials
                });
            case AuthenticationError.Banned:
                return BanRefusal(identity);
            case AuthenticationError.PendingRegistration:
                // Names an address that exists, unlike the refusal above, and leaves
                // no login record: an account never activated has no user row.
                _logger.IdentifierDisclosed(httpContext, "login", "PendingActivation");
                return new HttpBadRequestException(new Dictionary<string, string>
                {
                    ["email"] = RefusalMessage.RegistrationPending,
                    ["_pendingActivation"] = Marker
                });
            case AuthenticationError.Removed:
                return new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccountRemoved);
            case AuthenticationError.AccountLocked:
                // The number travels, the screen words the wait as for the rate limit.
                return new HttpException(HttpStatusCode.Forbidden, RefusalMessage.LoginLocked)
                {
                    Extensions = new Dictionary<string, object?>
                    {
                        ["retryAfterSeconds"] = identity.LockoutSecondsRemaining
                    }
                };
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void CountFailure(IIdentity identity) =>
        AuthenticationMetrics.LoginFailed.Add(1, AuthenticationMetrics.Reason(identity.Error.ToString()));

    /// <summary>
    /// Hands the attempt to the account layer with the two facts only the request
    /// carries; the rest is the account layer's decision.
    /// </summary>
    private Task RecordLoginAttempt(string email, HttpContext httpContext, bool isSuccessful) =>
        _loginRecordService.RecordAttempt(
            isSuccessful ? _identityProvider.Current.User.UserId : (Guid?)null,
            email,
            httpContext.GetClientAddress(),
            httpContext.Request.Headers.UserAgent.ToString(),
            isSuccessful);

    /// <summary>
    /// Finish a login the password step left owing a second factor
    /// </summary>
    /// <param name="request">Code from the device or a recovery code</param>
    /// <param name="httpContext">HTTP context</param>
    /// <returns>Login response with user and preferences</returns>
    public async Task<LoginResponse> CompleteTwoFactor(
        TwoFactorLoginRequest request, HttpContext httpContext)
    {
        // A cookie that is missing, forged or stale is answered exactly as a
        // wrong code: five ways of failing the second factor, one sentence.
        var challengeId = await _challengeCookie.Read(httpContext);
        if (challengeId == null)
        {
            throw RejectSecondFactor();
        }

        var identity = await _authenticationService.Authenticate(
            new SecondFactorCredentials { ChallengeId = challengeId.Value, Code = request.Code },
            httpContext);

        if (identity.Error != AuthenticationError.NoError)
        {
            CountFailure(identity);
        }

        switch (identity.Error)
        {
            case AuthenticationError.NoError:
                // The challenge is spent, and the cookie that carried it goes with it.
                _challengeCookie.Clear(httpContext);
                await RecordLoginAttempt(
                    _identityProvider.Current.User.Email ?? string.Empty, httpContext,
                    isSuccessful: true);
                return await SignedInResponse();
            case AuthenticationError.Banned:
                _challengeCookie.Clear(httpContext);
                throw BanRefusal(identity);
            case AuthenticationError.Removed:
                _challengeCookie.Clear(httpContext);
                throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccountRemoved);
            default:
                throw RejectSecondFactor();
        }
    }

    /// <summary>
    /// The refusal a ban comes back as, with the day it runs out beside it.
    /// </summary>
    /// <remarks>
    /// The moment travels and the screen words it. It comes with the identity: a
    /// refused identity carries the guest, whose restrictions are empty.
    /// </remarks>
    private static HttpException BanRefusal(IIdentity identity) =>
        new(HttpStatusCode.Forbidden, RefusalMessage.AccountBanned)
        {
            Extensions = new Dictionary<string, object?>
            {
                ["banExpiresUtc"] = identity.BanEndsUtc
            }
        };

    /// <summary>
    /// The one refusal every way of failing the second factor comes back as.
    /// </summary>
    /// <remarks>
    /// Byte for byte the same answer for a wrong code, an expired challenge, an
    /// unknown one, a spent recovery code and a challenge out of attempts. The
    /// cookie is deliberately left alone: clearing it only where the challenge
    /// is really gone would make the response distinguishable by its headers.
    /// </remarks>
    private static HttpBadRequestException RejectSecondFactor() =>
        new(new Dictionary<string, string> { ["code"] = RefusalMessage.TwoFactorRejected });

    /// <summary>
    /// Logout current session
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    public Task Logout(HttpContext httpContext) => _authenticationService.Logout(httpContext);

    /// <summary>
    /// Logout from all devices except current, which stays as it is
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    public Task LogoutElsewhere(HttpContext httpContext) => _authenticationService.LogoutElsewhere(httpContext);

    /// <summary>
    /// Get all active sessions for current user
    /// </summary>
    /// <returns>List of sessions</returns>
    public async Task<IEnumerable<Session>> GetSessions()
    {
        var sessions = await _sessionService.GetCurrentUserSessions();

        return sessions.Select(s => new Session
        {
            Id = s.Id,
            IsCurrent = s.IsCurrent,
            Persistent = s.Persistent,
            ExpirationUtc = s.ExpirationUtc,
            CreatedUtc = s.CreatedUtc,
            DeviceInfo = s.DeviceInfo,
            IpAddress = s.IpAddress
        }).ToList();
    }

    /// <summary>
    /// Terminate a specific session
    /// </summary>
    /// <param name="sessionId">Session ID to terminate</param>
    public async Task TerminateSession(Guid sessionId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        await _sessionService.TerminateSession(currentUserId, sessionId);
    }
}
