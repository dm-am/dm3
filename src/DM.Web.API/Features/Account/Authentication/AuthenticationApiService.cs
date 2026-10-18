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
using User = DM.Web.API.Features.Community.Users.User;

namespace DM.Web.API.Features.Account.Authentication;

/// <inheritdoc />
internal class AuthenticationApiService : IAuthenticationApiService
{
    private readonly IWebAuthenticationService _authenticationService;
    private readonly IAuthenticationService _coreAuthenticationService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserService _userService;
    private readonly ILoginRecordService _loginRecordService;
    private readonly UserMapper _mapper;
    private readonly PreferencesMapper _preferencesMapper;
    private readonly TwoFactorChallengeCookie _challengeCookie;
    private readonly ILogger<AuthenticationApiService> _logger;
    private readonly TwoFactorConfiguration _twoFactorConfig;

    /// <summary>
    /// Creates a new instance of AuthenticationApiService
    /// </summary>
    public AuthenticationApiService(
        IWebAuthenticationService authenticationService,
        IAuthenticationService coreAuthenticationService,
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
        _coreAuthenticationService = coreAuthenticationService;
        _identityProvider = identityProvider;
        _userService = userService;
        _loginRecordService = loginRecordService;
        _mapper = mapper;
        _preferencesMapper = preferencesMapper;
        _challengeCookie = challengeCookie;
        _logger = logger;
        _twoFactorConfig = twoFactorConfig.Value;
    }

    /// <inheritdoc />
    public async Task<LoginResponse> Login(LoginRequest request, HttpContext httpContext)
    {
        // Map API DTO to internal credentials
        var credentials = new LoginCredentials
        {
            Email = request.Email,
            Password = request.Password,
            RememberMe = request.RememberMe
        };

        var identity = await _authenticationService.Authenticate(credentials, httpContext);

        // The password was right and the login is not over. Nothing about the
        // account travels back: no user, no preferences, no session cookie -
        // only the stage, and the challenge in a cookie of its own.
        //
        // Not counted as a successful login either: it is not one yet, and the
        // record of it is written by the step that finishes it.
        if (identity.TwoFactorChallengeId.HasValue)
        {
            await _challengeCookie.Write(httpContext, identity.TwoFactorChallengeId.Value,
                TimeSpan.FromMinutes(_twoFactorConfig.ChallengeLifetimeMinutes));
            return new LoginResponse { TwoFactorRequired = true };
        }

        // Record login attempt for moderation (fire-and-forget, non-blocking)
        await RecordLoginAttempt(request.Email, httpContext,
            isSuccessful: identity.Error == AuthenticationError.NoError);

        // Once, above the switch, rather than in each refusing branch: what makes
        // this readable is that every refusal is counted under its own reason, and
        // a branch added below without a line of its own would silently drop out
        // of the count while every rule over it stayed green.
        if (identity.Error != AuthenticationError.NoError)
        {
            AuthenticationMetrics.LoginFailed.Add(1,
                AuthenticationMetrics.Reason(identity.Error.ToString()));
        }

        switch (identity.Error)
        {
            case AuthenticationError.NoError:
                var userDetails = await _userService.GetDetailsAsync(_identityProvider.Current.User.Username);
                var response = new LoginResponse
                {
                    // The viewer the client adopts on sign-in, and it has to
                    // arrive knowing whether the rank is in force: nothing
                    // re-reads the viewer between here and the first moderation
                    // page. Read off the identity because the details come from
                    // the database, which holds the recorded role and knows
                    // nothing of the fold.
                    User = _mapper.ToUser(userDetails),
                    Preferences = _preferencesMapper.ToPreferences(userDetails.Settings)
                };
                return response;
            case AuthenticationError.WrongLogin:
            case AuthenticationError.WrongPassword:
                // Unified error message to prevent user enumeration attacks
                throw new HttpBadRequestException(new Dictionary<string, string>
                {
                    ["password"] = RefusalMessage.WrongCredentials
                });
            case AuthenticationError.Banned:
                throw BanRefusal(identity);
            case AuthenticationError.PendingRegistration:
                // This answer names an address that exists, which is the whole
                // difference between it and the unified refusal above it: a caller
                // walking a list learns which addresses are registered, and the
                // login records hold nothing about the attempt because an account
                // that never activated has no user row to file it under.
                _logger.IdentifierDisclosed(httpContext, "login", "PendingActivation");
                throw new HttpBadRequestException(new Dictionary<string, string>
                {
                    ["email"] = "Регистрация не завершена",
                    ["_pendingActivation"] = "true"
                });
            case AuthenticationError.Removed:
                throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.AccountRemoved);
            // These refusals are the title of a problem document, which the client
            // prints as one line under the form. One sentence, and a sentence
            // standing alone carries no full stop.
            case AuthenticationError.AccountLocked:
                // The number travels and the sentence does not: the screen says
                // the wait in the words it already uses for the rate limit, and
                // a second copy of that sentence here is the one that drifts.
                throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.LoginLocked)
                {
                    Extensions = new Dictionary<string, object?>
                    {
                        ["retryAfterSeconds"] = identity.LockoutSecondsRemaining
                    }
                };
            // Unreachable from the password step, which never returns it, and
            // written out because the switch is what keeps every refusal counted
            // under a reason of its own.
            case AuthenticationError.TwoFactorRejected:
                throw new HttpBadRequestException(new Dictionary<string, string>
                {
                    ["code"] = RefusalMessage.TwoFactorRejected
                });
            // Forbidden is missing from this switch on purpose. The only thing
            // that returns it is the passwordless session, which the password
            // step never asks for, so it cannot reach a reader of this card and
            // had no business carrying a sentence written for one. Should the
            // password path ever return it, the throw below is the loud answer
            // that sends someone to read this comment.
            default:
                throw new ArgumentOutOfRangeException();
        }
    }


    /// <summary>
    /// Hands the attempt to the account layer with the two facts only the request
    /// carries. Which identity a failure is filed under, how much of the user agent
    /// is kept and what a failed write costs are decisions about the account, and
    /// they lived here until the layer below had no say in them at all.
    /// </summary>
    private Task RecordLoginAttempt(string email, HttpContext httpContext, bool isSuccessful) =>
        _loginRecordService.RecordAttempt(
            isSuccessful ? _identityProvider.Current.User.UserId : (Guid?)null,
            email,
            httpContext.GetClientAddress(),
            httpContext.Request.Headers.UserAgent.ToString(),
            isSuccessful);

    /// <inheritdoc />
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
            AuthenticationMetrics.LoginFailed.Add(1,
                AuthenticationMetrics.Reason(identity.Error.ToString()));
        }

        switch (identity.Error)
        {
            case AuthenticationError.NoError:
                // The challenge is spent whichever way this ends, and the cookie
                // that carried it goes with it.
                _challengeCookie.Clear(httpContext);
                await RecordLoginAttempt(
                    _identityProvider.Current.User.Email ?? string.Empty, httpContext,
                    isSuccessful: true);

                var userDetails = await _userService.GetDetailsAsync(_identityProvider.Current.User.Username);
                return new LoginResponse
                {
                    // False here by construction - the factor was just passed -
                    // and said out loud rather than left absent: the client is
                    // replacing a viewer that may have been stored withheld.
                    User = _mapper.ToUser(userDetails),
                    Preferences = _preferencesMapper.ToPreferences(userDetails.Settings)
                };
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
    /// The screen spells the day itself, in the reader's own time zone and in
    /// its own words, so what travels is the moment and not a sentence. The
    /// moment comes with the identity: the domain read the ban off the account
    /// it refused, and a refused identity carries the guest, whose list of
    /// restrictions is empty - computed here, every ban read as one with no end.
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

    /// <inheritdoc />
    public Task Logout(HttpContext httpContext) => _authenticationService.Logout(httpContext);

    /// <inheritdoc />
    public Task LogoutElsewhere(HttpContext httpContext) => _authenticationService.LogoutElsewhere(httpContext);

    /// <inheritdoc />
    public async Task<IEnumerable<Session>> GetSessions()
    {
        var sessions = await _coreAuthenticationService.GetCurrentUserSessions();

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

    /// <inheritdoc />
    public async Task TerminateSession(Guid sessionId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        await _coreAuthenticationService.TerminateSession(currentUserId, sessionId);
    }
}
