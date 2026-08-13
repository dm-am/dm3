using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
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
    private readonly IMapper _mapper;
    private readonly ILogger<AuthenticationApiService> _logger;

    /// <summary>
    /// Creates a new instance of AuthenticationApiService
    /// </summary>
    public AuthenticationApiService(
        IWebAuthenticationService authenticationService,
        IAuthenticationService coreAuthenticationService,
        IIdentityProvider identityProvider,
        IUserService userService,
        ILoginRecordService loginRecordService,
        IMapper mapper,
        ILogger<AuthenticationApiService> logger)
    {
        _authenticationService = authenticationService;
        _coreAuthenticationService = coreAuthenticationService;
        _identityProvider = identityProvider;
        _userService = userService;
        _loginRecordService = loginRecordService;
        _mapper = mapper;
        _logger = logger;
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
                    User = _mapper.Map<User>(userDetails),
                    Preferences = new Preferences
                    {
                        Theme = userDetails.Settings.Theme,
                        Paging = _mapper.Map<Paging>(userDetails.Settings.Paging)
                    }
                };
                return response;
            case AuthenticationError.WrongLogin:
            case AuthenticationError.WrongPassword:
                // Unified error message to prevent user enumeration attacks
                throw new HttpBadRequestException(new Dictionary<string, string>
                {
                    ["password"] = "Неверная почта или пароль"
                });
            // "Забанен", not "заблокирован": the security history captions the
            // lockout after failed attempts "Аккаунт заблокирован", and that is
            // the AccountLocked branch of this very switch. One word, one
            // meaning — the rest of the product calls this a ban.
            case AuthenticationError.Banned:
                throw new HttpException(HttpStatusCode.Forbidden,
                    "Аккаунт забанен");
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
                throw new HttpException(HttpStatusCode.Forbidden,
                    "Аккаунт удален");
            case AuthenticationError.Forbidden:
                throw new HttpException(HttpStatusCode.Forbidden,
                    "Ошибка авторизации.");
            case AuthenticationError.AccountLocked:
                throw new HttpException(HttpStatusCode.Forbidden,
                    "Слишком много неудачных попыток. Попробуйте позже.");
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
