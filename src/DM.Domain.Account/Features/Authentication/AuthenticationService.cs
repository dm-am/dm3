using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.Authentication;

/// <inheritdoc />
internal class AuthenticationService : IAuthenticationService
{
    private readonly ISecurityManager _securityManager;
    private readonly ISymmetricCryptoService _cryptoService;
    private readonly IAuthenticationRepository _repository;
    private readonly ISessionFactory _sessionFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IIdentityProvider _identityProvider;
    private readonly ILoginAttemptTracker _loginAttemptTracker;
    private readonly ISecurityAuditService _auditService;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly AuthenticationConfiguration _config;

    private const string UserIdKey = "userId";
    private const string SessionIdKey = "sessionId";
    private const string TimestampKey = "ts";
    private const string TransferKey = "transfer";
    private const int TransferTokenValidityMinutes = 5;

    /// <inheritdoc />
    public AuthenticationService(
        ISecurityManager securityManager,
        ISymmetricCryptoService cryptoService,
        IAuthenticationRepository repository,
        ISessionFactory sessionFactory,
        IDateTimeProvider dateTimeProvider,
        IIdentityProvider identityProvider,
        ILoginAttemptTracker loginAttemptTracker,
        ISecurityAuditService auditService,
        ILogger<AuthenticationService> logger,
        IOptions<AuthenticationConfiguration> authConfig)
    {
        _securityManager = securityManager;
        _cryptoService = cryptoService;
        _repository = repository;
        _sessionFactory = sessionFactory;
        _dateTimeProvider = dateTimeProvider;
        _identityProvider = identityProvider;
        _loginAttemptTracker = loginAttemptTracker;
        _auditService = auditService;
        _logger = logger;
        _config = authConfig.Value;
    }

    /// <inheritdoc />
    public async Task<IIdentity> Authenticate(string email, string password, bool rememberMe = true, SessionContext? context = null)
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

            // Try to find user to log the event (may not exist)
            var (found, lockedUser) = await _repository.TryFindUserByEmail(email);
            if (found && lockedUser != null)
            {
                await _auditService.LogAsync(lockedUser.UserId, SecurityEventType.AccountLocked,
                    context?.IpAddress, context?.UserAgent, "Account locked due to failed attempts");
            }

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
            case true when !_securityManager.ComparePasswords(password, user.Salt, user.PasswordHash):
                await _loginAttemptTracker.RecordFailedAttempt(email);
                await _auditService.LogAsync(user.UserId, SecurityEventType.LoginFailure,
                    context?.IpAddress, context?.UserAgent, "Wrong password");
                _logger.LogWarning("Login failed: wrong password. UserId={UserId}, Email={Email}", user.UserId, email);
                return Identity.Fail(AuthenticationError.WrongPassword);

            default:
                // Successful login - reset attempt counter
                await _loginAttemptTracker.ResetAttempts(email);

                // Update activity on login
                await _repository.UpdateActivity(user.UserId, _dateTimeProvider.Now);

                // Session persistence based on "remember me" checkbox
                var session = _sessionFactory.Create(persistent: rememberMe, invisible: false, context);
                var settings = await _repository.FindUserSettings(user.UserId);

                // Audit log: successful login
                await _auditService.LogAsync(user.UserId, SecurityEventType.LoginSuccess,
                    context?.IpAddress, context?.UserAgent);

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
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or CryptographicException)
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

        if (session.ExpirationUtc < _dateTimeProvider.Now)
        {
            await _repository.RemoveSession(userId, sessionId);
            _logger.LogDebug("Token authentication failed: session expired. UserId={UserId}, SessionId={SessionId}", userId, sessionId);
            return Identity.Fail(AuthenticationError.SessionExpired);
        }

        // Sliding window: refresh session when approaching expiration
        var sessionRefreshDelta = TimeSpan.FromMinutes(_config.SessionRefreshMinutes);
        if (session.ExpirationUtc < _dateTimeProvider.Now + sessionRefreshDelta)
        {
            await _repository.RefreshSession(userId, sessionId, session.ExpirationUtc + sessionRefreshDelta);
        }

        var activityTrackingInterval = TimeSpan.FromMinutes(_config.ActivityTrackingMinutes);
        if (!session.Invisible && (
                !user.LastActivityUtc.HasValue ||
                _dateTimeProvider.Now - user.LastActivityUtc.Value > activityTrackingInterval))
        {
            await _repository.UpdateActivity(user.UserId, _dateTimeProvider.Now);
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
        await _repository.UpdateActivity(identity.User.UserId, _dateTimeProvider.Now);

        await _repository.RemoveSession(identity.User.UserId, identity.Session!.Id);

        // Audit log
        await _auditService.LogAsync(identity.User.UserId, SecurityEventType.Logout);

        _logger.LogInformation("User logged out. UserId={UserId}", identity.User.UserId);
        return Identity.Guest();
    }

    /// <inheritdoc />
    public async Task<IIdentity> LogoutElsewhere()
    {
        var identity = _identityProvider.Current;
        await _repository.RemoveSessionsExcept(identity.User.UserId, identity.Session!.Id);

        // Audit log
        await _auditService.LogAsync(identity.User.UserId, SecurityEventType.LogoutElsewhere);

        return identity;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Session>> GetCurrentUserSessions()
    {
        var identity = _identityProvider.Current;
        return await _repository.GetUserSessions(identity.User.UserId, identity.Session?.Id);
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

        // Audit log
        await _auditService.LogAsync(userId, SecurityEventType.SessionTerminated,
            details: $"SessionId: {sessionId}");

        _logger.LogInformation("Session terminated. UserId={UserId}, SessionId={SessionId}", userId, sessionId);
    }

    /// <inheritdoc />
    public async Task LogoutAll(Guid userId)
    {
        await _repository.RemoveAllSessions(userId);
        _logger.LogInformation("All sessions terminated for user. UserId={UserId}", userId);
    }

    private async Task<IIdentity> CreateAuthenticationResult(
        AuthenticatedUser user, CreateSession session, UserSettings settings)
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

    /// <inheritdoc />
    public async Task<string?> CreateTransferToken()
    {
        var identity = _identityProvider.Current;
        if (!identity.User.IsAuthenticated || identity.Session == null)
        {
            return null;
        }

        var transferData = new Dictionary<string, string>
        {
            [TransferKey] = "1",
            [UserIdKey] = identity.User.UserId.ToString(),
            [SessionIdKey] = identity.Session.Id.ToString(),
            [TimestampKey] = _dateTimeProvider.Now.ToUnixTimeSeconds().ToString()
        };

        return await _cryptoService.Encrypt(JsonSerializer.Serialize(transferData));
    }

    /// <inheritdoc />
    public async Task<IIdentity> AuthenticateWithTransferToken(string transferToken)
    {
        try
        {
            var decrypted = await _cryptoService.Decrypt(transferToken);
            var transferData = JsonSerializer.Deserialize<Dictionary<string, string>>(decrypted);

            if (transferData == null ||
                !transferData.ContainsKey(TransferKey) ||
                !transferData.TryGetValue(UserIdKey, out var userIdStr) ||
                !transferData.TryGetValue(SessionIdKey, out var sessionIdStr) ||
                !transferData.TryGetValue(TimestampKey, out var timestampStr))
            {
                _logger.LogWarning("Transfer token authentication failed: invalid format");
                return Identity.Fail(AuthenticationError.ForgedToken);
            }

            if (!Guid.TryParse(userIdStr, out var userId) ||
                !Guid.TryParse(sessionIdStr, out var sessionId) ||
                !long.TryParse(timestampStr, out var timestamp))
            {
                _logger.LogWarning("Transfer token authentication failed: invalid data");
                return Identity.Fail(AuthenticationError.ForgedToken);
            }

            // Check if token has expired
            var tokenTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
            if (_dateTimeProvider.Now - tokenTime > TimeSpan.FromMinutes(TransferTokenValidityMinutes))
            {
                _logger.LogWarning("Transfer token authentication failed: token expired. UserId={UserId}", userId);
                return Identity.Fail(AuthenticationError.SessionExpired);
            }

            // Verify user and session exist
            var user = await _repository.FindUser(userId);
            var session = await _repository.FindUserSession(sessionId);
            var settings = await _repository.FindUserSettings(userId);

            if (user == null)
            {
                _logger.LogWarning("Transfer token authentication failed: user not found. UserId={UserId}", userId);
                return Identity.Fail(AuthenticationError.SessionExpired);
            }

            if (user.IsRemoved)
            {
                _logger.LogWarning("Transfer token authentication failed: user removed. UserId={UserId}", userId);
                return Identity.Fail(AuthenticationError.Removed);
            }

            if (user.AccessPolicy.HasFlag(AccessPolicy.FullBan))
            {
                _logger.LogWarning("Transfer token authentication failed: user banned. UserId={UserId}", userId);
                return Identity.Fail(AuthenticationError.Banned);
            }

            if (session == null || session.ExpirationUtc < _dateTimeProvider.Now)
            {
                _logger.LogWarning("Transfer token authentication failed: session expired. UserId={UserId}", userId);
                return Identity.Fail(AuthenticationError.SessionExpired);
            }

            // Create a new auth token for this mirror
            var authData = new Dictionary<string, Guid>
            {
                [UserIdKey] = userId,
                [SessionIdKey] = sessionId
            };
            var newAuthToken = await _cryptoService.Encrypt(JsonSerializer.Serialize(authData));

            _logger.LogInformation("User authenticated via mirror transfer. UserId={UserId}", userId);
            return Identity.Success(user, session, settings, newAuthToken);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or CryptographicException)
        {
            _logger.LogWarning(ex, "Transfer token authentication failed: invalid token");
            return Identity.Fail(AuthenticationError.ForgedToken);
        }
    }
}
