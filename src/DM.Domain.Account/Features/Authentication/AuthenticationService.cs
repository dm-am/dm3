using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;

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
    private readonly ISecurityAuditRepository _auditService;
    private readonly IEventProducer _eventProducer;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly AuthenticationConfiguration _config;

    /// <summary>
    /// Credentials of nobody, hashed so that a login for an account that does not
    /// exist costs what a login for one that does costs.
    /// </summary>
    /// <remarks>
    /// The two answers are required to be indistinguishable, and in text they
    /// are: both come back as "wrong email or password". In time they were not. A
    /// missing account skipped Argon2id entirely and answered tens of
    /// milliseconds sooner, which says the address carries no account without any
    /// password having been tried. The salt has to be well formed because the
    /// hasher decodes it; neither value is ever stored or compared against
    /// anything real.
    /// </remarks>
    private static readonly string DecoySalt = Convert.ToBase64String(new byte[75]);

    /// <inheritdoc cref="DecoySalt" />
    private static readonly string DecoyHash = Convert.ToBase64String(new byte[32]);

    /// <inheritdoc />
    public AuthenticationService(
        ISecurityManager securityManager,
        ISymmetricCryptoService cryptoService,
        IAuthenticationRepository repository,
        ISessionFactory sessionFactory,
        IDateTimeProvider dateTimeProvider,
        IIdentityProvider identityProvider,
        ILoginAttemptTracker loginAttemptTracker,
        ISecurityAuditRepository auditService,
        IEventProducer eventProducer,
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
        _eventProducer = eventProducer;
        _logger = logger;
        _config = authConfig.Value;
    }

    /// <inheritdoc />
    public async Task<IIdentity> Authenticate(string email, string password, bool rememberMe = true,
        SessionContext? context = null, CancellationToken cancellationToken = default)
    {
        // 1. PENDING CHECK - fast path, no throttling needed
        // Pending registrations have no password to brute-force
        if (await _repository.IsPendingRegistration(email))
        {
            _logger.LogInformation("Login failed: pending registration");
            return Identity.Fail(AuthenticationError.PendingRegistration);
        }

        // 2. THROTTLING - only for actual login attempts.
        // Counted per account and address, not per account: see LoginAttemptOrigin.
        var origin = new LoginAttemptOrigin(email, context?.IpAddress);
        if (await _loginAttemptTracker.IsAccountLocked(origin))
        {
            var remainingSeconds = await _loginAttemptTracker.GetRemainingLockoutSeconds(origin);
            _logger.LogWarning("Login failed: account locked due to too many failed attempts. RemainingSeconds={RemainingSeconds}",
                remainingSeconds);

            // Try to find user to log the event (may not exist)
            var (found, lockedUser) = await _repository.TryFindUserByEmail(email);
            if (found && lockedUser != null)
            {
                await _auditService.LogAsync(lockedUser.UserId, SecurityEventType.AccountLocked,
                    context?.IpAddress, context?.UserAgent, "Account locked due to failed attempts");
            }

            return Identity.Fail(AuthenticationError.AccountLocked);
        }

        // Progressive delay for bot protection.
        //
        // Cancellable, and it is the one wait in this method long enough to matter:
        // sleeping through a request the caller has already dropped holds the
        // database connection and the scope of that request for the whole delay. A
        // caller who cancels wins nothing by it - the wait happens before the
        // password is looked at, so a cancelled attempt is not an attempt and is
        // never counted as one.
        var delaySeconds = await _loginAttemptTracker.GetDelayForUser(origin);
        if (delaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
        }

        // 3. FIND USER BY EMAIL
        var (userFound, user) = await _repository.TryFindUserByEmail(email);
        ApplyActiveBans(user);

        // One Argon2id, before anything branches on what was found.
        //
        // Every refusal below is required to cost the same, and in text they
        // already read the same; in time they did not. A missing account and the
        // system actor both reached their answer without hashing anything - the
        // actor because its branch short-circuited on the role - and came back
        // some hundred and fifty milliseconds sooner than a wrong password. That
        // difference names which addresses exist and which one belongs to the
        // robot, without a password having been tried at all.
        //
        // The actor is hashed against the decoy rather than against its own
        // columns because the seed leaves them empty: it has no password, and the
        // point here is to spend the time, not to ask a question with an answer.
        var (salt, hash) = userFound && user!.Role != UserRole.System
            ? (user.Salt, user.PasswordHash)
            : (DecoySalt, DecoyHash);
        var passwordMatches = _securityManager.ComparePasswords(password, salt, hash);

        switch (userFound)
        {
            case false:
                await _loginAttemptTracker.RecordFailedAttempt(origin);
                _logger.LogWarning("Login failed: user not found");
                return Identity.Fail(AuthenticationError.WrongLogin);
            // The system actor is refused by its role and not by its credentials,
            // and the check is written out rather than left to its empty columns:
            // an invariant living in two strings of a seed is one row away from
            // being no invariant at all. It stands after the hash above and not
            // instead of it - refused before paying, the robot's address answered
            // sooner than every other and was identifiable by that alone.
            //
            // Folded into this branch rather than answered separately so that the
            // attempt is counted, audited and reported exactly like a wrong password,
            // leaving the account indistinguishable from outside.
            case true when user!.Role == UserRole.System || !passwordMatches:
                await _loginAttemptTracker.RecordFailedAttempt(origin);

                // Only the attempt that crosses the threshold reaches this while
                // locked: every later one is refused above, before the counter is
                // touched. So the owner of the account hears about the lockout
                // once per lockout, through a channel the person guessing the
                // password does not see.
                if (await _loginAttemptTracker.IsAccountLocked(origin))
                {
                    await _eventProducer.SendAsync(EventType.AccountLocked, user.UserId);
                }

                await _auditService.LogAsync(user.UserId, SecurityEventType.LoginFailure,
                    context?.IpAddress, context?.UserAgent, "Wrong password");
                _logger.LogWarning("Login failed: wrong password. UserId={UserId}", user.UserId);
                return Identity.Fail(AuthenticationError.WrongPassword);

            // Past this branch the password is proven, and only past it may the
            // answer say anything about the account itself. Asked before it,
            // "removed" and "banned" told whoever typed an address what had been
            // done to the person behind it, without a password and without the
            // try being counted. That an address is registered is disclosed here
            // by a recorded decision (see SECURITY.md); what moderation did with
            // the account is not, and the owner still gets the real reason.
            case true when user.IsRemoved:
                _logger.LogWarning("Login failed: account removed. UserId={UserId}", user.UserId);
                return Identity.Fail(AuthenticationError.Removed);
            case true when user.AccessPolicy.HasFlag(AccessPolicy.FullBan):
                _logger.LogWarning("Login failed: account banned. UserId={UserId}", user.UserId);
                return Identity.Fail(AuthenticationError.Banned);

            default:
                // Successful login - reset attempt counter
                await _loginAttemptTracker.ResetAttempts(email);

                // Update activity on login
                await _repository.UpdateActivity(user.UserId, _dateTimeProvider.Now);

                // Session persistence based on "remember me" checkbox
                var session = _sessionFactory.Create(persistent: rememberMe, context: context);
                var settings = await _repository.FindUserSettings(user.UserId);

                // Awaited, and it has to be. The suspicious-login check runs
                // immediately after this method returns: it reads the trail newest
                // first and drops the top entry as "this login". Left unawaited,
                // the write races that read, and the check either meets a trail
                // without its own entry or throws away a genuine earlier login
                // instead. Waiting buys away no availability either - the journal,
                // the session written below and the attempt counter read at the
                // top of this method share one Mongo, so a login that got this far
                // never had Mongo down.
                await _auditService.LogAsync(user.UserId, SecurityEventType.LoginSuccess,
                    context?.IpAddress, context?.UserAgent);

                _logger.LogInformation("User authenticated successfully. UserId={UserId}", user.UserId);
                return await CreateAuthenticationResult(user, session, settings);
        }
    }

    /// <inheritdoc />
    public async Task<IIdentity> Authenticate(string authToken)
    {
        var token = await SessionToken.Read(_cryptoService, authToken);
        if (token == null)
        {
            _logger.LogWarning("Token authentication failed: forged or corrupted token");
            return Identity.Fail(AuthenticationError.ForgedToken);
        }

        var (userId, sessionId) = token;

        var fetchUser = _repository.FindUser(userId);
        var fetchSession = _repository.FindUserSession(userId, sessionId);
        var fetchSettings = _repository.FindUserSettings(userId);

        await Task.WhenAll(fetchUser, fetchSession, fetchSettings);

        var user = await fetchUser;
        var session = await fetchSession;
        var settings = await fetchSettings;
        ApplyActiveBans(user);

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

        // Sliding window with a ceiling, and the ceiling is the point. Extended from
        // its own expiry on every visit, a session had no last day at all: a token
        // taken once and used once a week outlives the account it belongs to, and
        // nothing in the model says when it should have stopped. The two settings
        // already name the lifetime a session was issued with, so the ceiling is
        // that lifetime counted from issue rather than a third number.
        var lifetime = session.Persistent
            ? TimeSpan.FromDays(_config.PersistentSessionExpirationDays)
            : TimeSpan.FromHours(_config.SessionExpirationHours);
        var ceiling = session.CreatedUtc + lifetime;

        var sessionRefreshDelta = TimeSpan.FromMinutes(_config.SessionRefreshMinutes);
        if (session.ExpirationUtc < _dateTimeProvider.Now + sessionRefreshDelta)
        {
            var extended = session.ExpirationUtc + sessionRefreshDelta;
            var refreshed = extended < ceiling ? extended : ceiling;

            // Only forward. Against the ceiling the two are equal, and writing it
            // again would be a database round trip on every request of the last
            // window of every session.
            if (refreshed > session.ExpirationUtc)
            {
                await _repository.RefreshSession(userId, sessionId, refreshed);
            }
        }

        var activityTrackingInterval = TimeSpan.FromMinutes(_config.ActivityTrackingMinutes);
        if (!user.LastActivityUtc.HasValue ||
            _dateTimeProvider.Now - user.LastActivityUtc.Value > activityTrackingInterval)
        {
            await _repository.UpdateActivity(user.UserId, _dateTimeProvider.Now);
        }

        return Identity.Success(user, session, settings, authToken);
    }

    /// <inheritdoc />
    public async Task<IIdentity> Authenticate(Guid userId, SessionContext? context = null)
    {
        var user = await _repository.FindUser(userId);
        if (user == null)
        {
            return Identity.Guest();
        }

        // This overload mints a real session without a password, so it needs the
        // same account-state gates as the other two: it is authentication, not a
        // lookup. Today only the activation auto-login reaches it, but nothing
        // about the signature says so.
        ApplyActiveBans(user);

        if (user.IsRemoved)
        {
            _logger.LogWarning("Direct authentication failed: user removed. UserId={UserId}", userId);
            return Identity.Fail(AuthenticationError.Removed);
        }

        if (user.AccessPolicy.HasFlag(AccessPolicy.FullBan))
        {
            _logger.LogWarning("Direct authentication failed: user banned. UserId={UserId}", userId);
            return Identity.Fail(AuthenticationError.Banned);
        }

        // An ordinary session, with the address and agent it was opened from.
        // Minted invisible and contextless, it was a login the account owner had
        // no way to see: no entry in the security journal, a device list row with
        // no address in it, and no activity stamp. It also left the account with
        // no successful login on record, so the first real login from anywhere
        // had nothing to be compared against.
        await _repository.UpdateActivity(userId, _dateTimeProvider.Now);
        var session = _sessionFactory.Create(persistent: false, context: context);
        var settings = await _repository.FindUserSettings(userId);

        await _auditService.LogAsync(userId, SecurityEventType.LoginSuccess,
            context?.IpAddress, context?.UserAgent);

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
            throw new HttpException(HttpStatusCode.Forbidden,
                "Завершить чужую сессию нельзя");
        }

        // Cannot terminate current session - use Logout instead
        if (identity.Session?.Id == sessionId)
        {
            // A plain InvalidOperationException reaches the client as 500: the error
            // middleware maps only HttpException and its kin. Terminating one's own
            // session is a caller mistake, not a server fault.
            throw new HttpException(HttpStatusCode.BadRequest,
                "Нельзя завершить текущую сессию. Для этого есть кнопка Выйти");
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
        var token = await new SessionToken(user.UserId, session.Id).Write(_cryptoService);
        return Identity.Success(user, newSession, settings, token);
    }

    /// <summary>
    /// Fold the bans that are in force right now into the policy every
    /// authorization check reads.
    /// </summary>
    /// <remarks>
    /// Bans live in their own table and nothing writes the user's own
    /// AccessPolicy column, so without this fold no ban restricts anything: not
    /// the ordinary ban at the content surfaces, not even the full ban at login.
    /// Doing it here, at the single point where an identity is built, is also
    /// what makes a ban start and stop by itself — an expired ban stops being in
    /// force on the next request with no job to run, and a ban issued mid-session
    /// takes effect on the next request rather than at session expiry.
    /// </remarks>
    private void ApplyActiveBans(AuthenticatedUser? user)
    {
        if (user == null)
        {
            return;
        }

        user.AccessPolicy = user.EffectiveAccessPolicyAt(_dateTimeProvider.Now);
    }
}
