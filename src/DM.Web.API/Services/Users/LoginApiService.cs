using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Authentication.Dto;
using DM.Services.Authentication.Implementation;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Authentication.Repositories;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Community.BusinessProcesses.Users.Updating;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using DM.Web.Core.Authentication;
using DM.Web.Core.Authentication.Credentials;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using User = DM.Web.API.Dto.Users.User;
using UserDetails = DM.Web.API.Dto.Users.UserDetails;
using ServiceUserSettings = DM.Services.Authentication.Dto.UserSettings;

namespace DM.Web.API.Services.Users;

/// <inheritdoc />
internal class LoginApiService : ILoginApiService
{
    private readonly IWebAuthenticationService authenticationService;
    private readonly IAuthenticationService coreAuthenticationService;
    private readonly IIdentityProvider identityProvider;
    private readonly IUserReadingService userReadingService;
    private readonly IUserUpdatingService userUpdatingService;
    private readonly ILoginRecordRepository loginRecordRepository;
    private readonly DmDbContext dbContext;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ILogger<LoginApiService> logger;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public LoginApiService(
        IWebAuthenticationService authenticationService,
        IAuthenticationService coreAuthenticationService,
        IIdentityProvider identityProvider,
        IUserReadingService userReadingService,
        IUserUpdatingService userUpdatingService,
        ILoginRecordRepository loginRecordRepository,
        DmDbContext dbContext,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        ILogger<LoginApiService> logger,
        IMapper mapper)
    {
        this.authenticationService = authenticationService;
        this.coreAuthenticationService = coreAuthenticationService;
        this.identityProvider = identityProvider;
        this.userReadingService = userReadingService;
        this.userUpdatingService = userUpdatingService;
        this.loginRecordRepository = loginRecordRepository;
        this.dbContext = dbContext;
        this.guidFactory = guidFactory;
        this.dateTimeProvider = dateTimeProvider;
        this.logger = logger;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Envelope<User>> Login(LoginCredentials credentials, HttpContext httpContext)
    {
        var identity = await authenticationService.Authenticate(credentials, httpContext);

        // Record login attempt for moderation (fire-and-forget, non-blocking)
        await RecordLoginAttempt(credentials.Email, httpContext,
            isSuccessful: identity.Error == AuthenticationError.NoError);

        switch (identity.Error)
        {
            case AuthenticationError.NoError:
                var userDetails = await userReadingService.GetDetails(identityProvider.Current.User.Login);
                return new Envelope<User>(mapper.Map<User>(userDetails));
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
                throw new HttpBadRequestException(new Dictionary<string, string>
                {
                    ["email"] = "Слишком много неудачных попыток. Попробуйте позже."
                });
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async Task RecordLoginAttempt(string login, HttpContext httpContext, bool isSuccessful)
    {
        try
        {
            // For successful logins, we have the user in identity provider
            Guid? userId = isSuccessful
                ? identityProvider.Current.User.UserId
                : await TryResolveUserId(login);

            if (!userId.HasValue)
                return;

            var ipAddress = ExtractClientIp(httpContext);
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();

            await loginRecordRepository.Record(new UserLoginRecord
            {
                UserLoginRecordId = guidFactory.Create(),
                UserId = userId.Value,
                IpAddress = ipAddress,
                UserAgent = userAgent.Length > 500 ? userAgent[..500] : userAgent,
                LoginUtc = dateTimeProvider.Now,
                IsSuccessful = isSuccessful
            });
        }
        catch (Exception ex)
        {
            // Login recording is non-critical — never block the login flow
            logger.LogWarning(ex, "Failed to record login attempt for {Login}", login);
        }
    }

    private Task<Guid?> TryResolveUserId(string login) =>
        loginRecordRepository.TryResolveUserId(login);

    private static string ExtractClientIp(HttpContext httpContext)
    {
        // X-Forwarded-For for reverse proxy (nginx, cloudflare)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP (client), the rest are proxies
            var clientIp = forwardedFor.Split(',', StringSplitOptions.TrimEntries).First();
            if (IPAddress.TryParse(clientIp, out _))
                return clientIp;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <inheritdoc />
    public Task Logout(HttpContext httpContext) => authenticationService.Logout(httpContext);

    /// <inheritdoc />
    public Task LogoutAll(HttpContext httpContext) => authenticationService.LogoutElsewhere(httpContext);

    /// <inheritdoc />
    public async Task<Envelope<UserDetails>> GetCurrent()
    {
        var userDetails = await userReadingService.GetDetails(identityProvider.Current.User.Login);
        return new Envelope<UserDetails>(mapper.Map<UserDetails>(userDetails));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<SessionInfo>> GetSessions()
    {
        var currentSessionId = identityProvider.Current.Session!.Id;
        var sessions = await coreAuthenticationService.GetCurrentUserSessions();

        var sessionInfos = sessions.Select(s => new SessionInfo
        {
            Id = s.Id,
            IsCurrent = s.Id == currentSessionId,
            Persistent = s.Persistent,
            ExpirationDate = s.ExpirationDate
        }).ToList();

        return new ListEnvelope<SessionInfo>(sessionInfos);
    }

    /// <inheritdoc />
    public async Task<Envelope<Dto.Users.UserSettings>> UpdateSettings(Dto.Users.UserSettings settings)
    {
        var currentUser = identityProvider.Current.User;

        var updateUser = new UpdateUser
        {
            Login = currentUser.Login,
            Settings = new ServiceUserSettings
            {
                ColorSchema = settings.ColorSchema,
                MentorGreetingsMessage = settings.MentorGreetingsMessage,
                Paging = new PagingSettings
                {
                    TopicsPerPage = settings.PagingLimits?.TopicsPerPage ?? 10,
                    CommentsPerPage = settings.PagingLimits?.CommentsPerPage ?? 10,
                    PostsPerPage = settings.PagingLimits?.PostsPerPage ?? 10,
                    MessagesPerPage = settings.PagingLimits?.MessagesPerPage ?? 10,
                    EntitiesPerPage = settings.PagingLimits?.EntitiesPerPage ?? 10
                }
            }
        };

        var updatedUser = await userUpdatingService.Update(updateUser);
        return new Envelope<Dto.Users.UserSettings>(mapper.Map<Dto.Users.UserSettings>(updatedUser.Settings));
    }

    /// <inheritdoc />
    public async Task TerminateSession(Guid sessionId)
    {
        var currentUserId = identityProvider.Current.User.UserId;
        await coreAuthenticationService.TerminateSession(currentUserId, sessionId);
    }
}