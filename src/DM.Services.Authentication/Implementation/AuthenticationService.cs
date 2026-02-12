using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Services.Authentication.Configuration;
using DM.Services.Authentication.Dto;
using DM.Services.Authentication.Factories;
using DM.Services.Authentication.Implementation.Security;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Authentication.Repositories;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DbSession = DM.Services.DataAccess.BusinessObjects.Users.Session;

namespace DM.Services.Authentication.Implementation;

/// <inheritdoc />
internal class AuthenticationService : IAuthenticationService
{
    private readonly ISecurityManager _securityManager;
    private readonly ISymmetricCryptoService _cryptoService;
    private readonly IAuthenticationRepository _repository;
    private readonly ISessionFactory _sessionFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly ILoginAttemptTracker _loginAttemptTracker;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly AuthenticationConfiguration _config;

    private const string UserIdKey = "userId";
    private const string SessionIdKey = "sessionId";

    /// <inheritdoc />
    public AuthenticationService(
        ISecurityManager securityManager,
        ISymmetricCryptoService cryptoService,
        IAuthenticationRepository repository,
        ISessionFactory sessionFactory,
        IDateTimeProvider dateTimeProvider,
        IIdentityProvider identityProvider,
        IUpdateBuilderFactory updateBuilderFactory,
        ILoginAttemptTracker loginAttemptTracker,
        ILogger<AuthenticationService> logger,
        IOptions<AuthenticationConfiguration> authConfig)
    {
        _securityManager = securityManager;
        _cryptoService = cryptoService;
        _repository = repository;
        _sessionFactory = sessionFactory;
        _dateTimeProvider = dateTimeProvider;
        _identityProvider = identityProvider;
        _updateBuilderFactory = updateBuilderFactory;
        _loginAttemptTracker = loginAttemptTracker;
        _logger = logger;
        _config = authConfig.Value;
    }

    /// <inheritdoc />
    public async Task<IIdentity> Authenticate(string email, string password, bool rememberMe = true)
    {
        // 1. PENDING CHECK - fast path, no throttling needed
        // Pending registrations have no password to brute-force
        if (await _repository.IsPendingRegistration(email))
        {
            _logger.LogInformation("Login failed: pending registration. Email={Email}", email);
            return Identity.Fail(AuthenticationError.PendingRegistration);
        }

        // 2. THROTTLING - only for actual login attempts
        if (await _loginAttemptTracker.IsAccountLocked(email))
        {
            var remainingSeconds = await _loginAttemptTracker.GetRemainingLockoutSeconds(email);
            _logger.LogWarning("Login failed: account locked due to too many failed attempts. Email={Email}, RemainingSeconds={RemainingSeconds}",
                email, remainingSeconds);
            return Identity.Fail(AuthenticationError.AccountLocked);
        }

        // Progressive delay for bot protection
        var delaySeconds = await _loginAttemptTracker.GetDelayForUser(email);
        if (delaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        }

        // 3. FIND USER BY EMAIL
        var (userFound, user) = await _repository.TryFindUserByEmail(email);

        switch (userFound)
        {
            case false:
                await _loginAttemptTracker.RecordFailedAttempt(email);
                _logger.LogWarning("Login failed: user not found. Email={Email}", email);
                return Identity.Fail(AuthenticationError.WrongLogin);
            case true when user!.IsRemoved:
                _logger.LogWarning("Login failed: account removed. UserId={UserId}, Email={Email}", user.UserId, email);
                return Identity.Fail(AuthenticationError.Removed);
            case true when user.AccessPolicy.HasFlag(AccessPolicy.FullBan):
                _logger.LogWarning("Login failed: account banned. UserId={UserId}, Email={Email}", user.UserId, email);
                return Identity.Fail(AuthenticationError.Banned);
            case true when !_securityManager.ComparePasswords(password, user.Salt, user.PasswordHash, user.PasswordHashVersion):
                await _loginAttemptTracker.RecordFailedAttempt(email);
                _logger.LogWarning("Login failed: wrong password. UserId={UserId}, Email={Email}", user.UserId, email);
                return Identity.Fail(AuthenticationError.WrongPassword);

            default:
                // Successful login - reset attempt counter
                await _loginAttemptTracker.ResetAttempts(email);

                // Opportunistic rehashing: upgrade password hash on successful login
                if (_securityManager.NeedsRehash(user.PasswordHashVersion))
                {
                    await RehashPassword(user.UserId, password);
                    _logger.LogInformation("Password hash upgraded for user. UserId={UserId}", user.UserId);
                }

                // Update activity on login
                var userUpdate = _updateBuilderFactory.Create<User>(user.UserId)
                    .Field(u => u.LastActivityUtc, _dateTimeProvider.Now);
                await _repository.UpdateActivity(userUpdate);

                // Session persistence based on "remember me" checkbox
                var session = _sessionFactory.Create(persistent: rememberMe, invisible: false);
                var settings = await _repository.FindUserSettings(user.UserId);
                _logger.LogInformation("User authenticated successfully. UserId={UserId}, Email={Email}",
                    user.UserId, email);
                return await CreateAuthenticationResult(user, session, settings);
        }
    }

    /// <inheritdoc />
    public async Task<IIdentity> Authenticate(string authToken)
    {
        Guid userId;
        Guid sessionId;

        try
        {
            var decryptedString = await _cryptoService.Decrypt(authToken);
            var authData = JsonSerializer.Deserialize<Dictionary<string, Guid>>(decryptedString);
            userId = authData![UserIdKey];
            sessionId = authData[SessionIdKey];
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or System.Security.Cryptography.CryptographicException)
        {
            _logger.LogWarning(ex, "Token authentication failed: forged or corrupted token");
            return Identity.Fail(AuthenticationError.ForgedToken);
        }

        var fetchUser = _repository.FindUser(userId);
        var fetchSession = _repository.FindUserSession(sessionId);
        var fetchSettings = _repository.FindUserSettings(userId);

        await Task.WhenAll(fetchUser, fetchSession, fetchSettings);

        var user = await fetchUser;
        var session = await fetchSession;
        var settings = await fetchSettings;

        // Validate user state (could have changed since token was issued)
        if (user == null)
        {
            _logger.LogWarning("Token authentication failed: user not found. UserId={UserId}", userId);
            return Identity.Fail(AuthenticationError.SessionExpired);
        }

        if (user.IsRemoved)
        {
            _logger.LogWarning("Token authentication failed: user removed. UserId={UserId}", userId);
            return Identity.Fail(AuthenticationError.Removed);
        }

        if (user.AccessPolicy.HasFlag(AccessPolicy.FullBan))
        {
            _logger.LogWarning("Token authentication failed: user banned. UserId={UserId}", userId);
            return Identity.Fail(AuthenticationError.Banned);
        }

        if (session == null)
        {
            _logger.LogDebug("Token authentication failed: session not found. UserId={UserId}, SessionId={SessionId}", userId, sessionId);
            return Identity.Fail(AuthenticationError.SessionExpired);
        }

        if (session.ExpirationDate < _dateTimeProvider.Now)
        {
            await _repository.RemoveSession(userId, sessionId);
            _logger.LogDebug("Token authentication failed: session expired. UserId={UserId}, SessionId={SessionId}", userId, sessionId);
            return Identity.Fail(AuthenticationError.SessionExpired);
        }

        // Sliding window: refresh session when approaching expiration
        var sessionRefreshDelta = TimeSpan.FromMinutes(_config.SessionRefreshMinutes);
        if (session.ExpirationDate < _dateTimeProvider.Now + sessionRefreshDelta)
        {
            await _repository.RefreshSession(userId, sessionId, session.ExpirationDate + sessionRefreshDelta);
        }

        var activityTrackingInterval = TimeSpan.FromMinutes(_config.ActivityTrackingMinutes);
        if (!session.Invisible && (
                !user.LastActivityUtc.HasValue ||
                _dateTimeProvider.Now - user.LastActivityUtc.Value > activityTrackingInterval))
        {
            var userUpdate = _updateBuilderFactory.Create<User>(user.UserId)
                .Field(u => u.LastActivityUtc, _dateTimeProvider.Now);
            await _repository.UpdateActivity(userUpdate);
        }

        return Identity.Success(user, session, settings, authToken);
    }

    /// <inheritdoc />
    public async Task<IIdentity> Authenticate(Guid userId)
    {
        var user = await _repository.FindUser(userId);
        if (user == null)
        {
            return Identity.Guest();
        }

        var session = _sessionFactory.Create(false, true);
        var settings = await _repository.FindUserSettings(userId);
        return await CreateAuthenticationResult(user, session, settings);
    }

    /// <inheritdoc />
    public async Task<IIdentity> Logout()
    {
        var identity = _identityProvider.Current;

        // Update activity on logout - this marks the last moment user was active
        var userUpdate = _updateBuilderFactory.Create<User>(identity.User.UserId)
            .Field(u => u.LastActivityUtc, _dateTimeProvider.Now);
        await _repository.UpdateActivity(userUpdate);

        await _repository.RemoveSession(identity.User.UserId, identity.Session!.Id);
        _logger.LogInformation("User logged out. UserId={UserId}", identity.User.UserId);
        return Identity.Guest();
    }

    /// <inheritdoc />
    public async Task<IIdentity> LogoutElsewhere()
    {
        var identity = _identityProvider.Current;
        await _repository.RemoveSessionsExcept(identity.User.UserId, identity.Session!.Id);
        return identity;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Dto.Session>> GetCurrentUserSessions()
    {
        var identity = _identityProvider.Current;
        return await _repository.GetUserSessions(identity.User.UserId);
    }

    /// <inheritdoc />
    public async Task TerminateSession(Guid userId, Guid sessionId)
    {
        var identity = _identityProvider.Current;

        // Security check: can only terminate own sessions
        if (identity.User.UserId != userId)
        {
            throw new UnauthorizedAccessException("Cannot terminate sessions of other users");
        }

        // Cannot terminate current session - use Logout instead
        if (identity.Session?.Id == sessionId)
        {
            throw new InvalidOperationException("Cannot terminate current session. Use logout instead.");
        }

        await _repository.RemoveSession(userId, sessionId);
        _logger.LogInformation("Session terminated. UserId={UserId}, SessionId={SessionId}", userId, sessionId);
    }

    /// <inheritdoc />
    public async Task LogoutAll(Guid userId)
    {
        await _repository.RemoveAllSessions(userId);
        _logger.LogInformation("All sessions terminated for user. UserId={UserId}", userId);
    }

    private async Task RehashPassword(Guid userId, string password)
    {
        var (hash, salt, version) = _securityManager.GeneratePassword(password);
        var userUpdate = _updateBuilderFactory.Create<User>(userId)
            .Field(u => u.PasswordHash, hash)
            .Field(u => u.Salt, salt)
            .Field(u => u.PasswordHashVersion, version);
        await _repository.UpdateActivity(userUpdate);
    }

    private async Task<IIdentity> CreateAuthenticationResult(
        AuthenticatedUser user, DbSession session, UserSettings settings)
    {
        var newSession = await _repository.AddSession(user.UserId, session);
        var authData = new Dictionary<string, Guid>
        {
            [UserIdKey] = user.UserId,
            [SessionIdKey] = session.Id
        };
        var token = await _cryptoService.Encrypt(JsonSerializer.Serialize(authData));
        return Identity.Success(user, newSession, settings, token);
    }
}
