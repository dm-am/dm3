using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;
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
    private readonly ILoginRecordRepository _loginRecordRepository;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<AuthenticationApiService> _logger;
    private readonly IMapper _mapper;

    /// <summary>
    /// Creates a new instance of AuthenticationApiService
    /// </summary>
    public AuthenticationApiService(
        IWebAuthenticationService authenticationService,
        IAuthenticationService coreAuthenticationService,
        IIdentityProvider identityProvider,
        IUserService userService,
        ILoginRecordRepository loginRecordRepository,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        ILogger<AuthenticationApiService> logger,
        IMapper mapper)
    {
        _authenticationService = authenticationService;
        _coreAuthenticationService = coreAuthenticationService;
        _identityProvider = identityProvider;
        _userService = userService;
        _loginRecordRepository = loginRecordRepository;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _mapper = mapper;
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
            case AuthenticationError.Banned:
                throw new HttpException(HttpStatusCode.Forbidden,
                    "Аккаунт заблокирован.");
            case AuthenticationError.PendingRegistration:
                throw new HttpBadRequestException(new Dictionary<string, string>
                {
                    ["email"] = "Регистрация не завершена",
                    ["_pendingActivation"] = "true"
                });
            case AuthenticationError.Removed:
                throw new HttpException(HttpStatusCode.Forbidden,
                    "Аккаунт удален.");
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

    private async Task RecordLoginAttempt(string email, HttpContext httpContext, bool isSuccessful)
    {
        try
        {
            // For successful logins, we have the user in identity provider
            Guid? userId = isSuccessful
                ? _identityProvider.Current.User.UserId
                : await TryResolveUserId(email);

            if (!userId.HasValue)
                return;

            var ipAddress = httpContext.GetClientAddress();
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();

            await _loginRecordRepository.Record(new UserLoginRecord
            {
                UserLoginRecordId = _guidFactory.Create(),
                UserId = userId.Value,
                IpAddress = ipAddress,
                UserAgent = userAgent.Length > 500 ? userAgent[..500] : userAgent,
                LoginUtc = _dateTimeProvider.Now,
                IsSuccessful = isSuccessful
            });
        }
        catch (Exception ex)
        {
            // Login recording is non-critical — never block the login flow.
            // The address is not in the message: it identifies a person and the log store
            // has no retention. The trace id and the security audit log carry the rest.
            _logger.LogWarning(ex, "Failed to record login attempt");
        }
    }

    private Task<Guid?> TryResolveUserId(string email) =>
        _loginRecordRepository.TryResolveUserId(email);

    /// <inheritdoc />
    public Task Logout(HttpContext httpContext) => _authenticationService.Logout(httpContext);

    /// <inheritdoc />
    public Task LogoutAll(HttpContext httpContext) => _authenticationService.LogoutElsewhere(httpContext);

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
