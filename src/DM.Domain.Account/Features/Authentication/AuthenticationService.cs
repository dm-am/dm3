using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;
using System;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.TwoFactor;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
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
    private readonly ILoginAttemptTracker _loginAttemptTracker;
    private readonly ISecurityAuditRepository _auditRepository;
    private readonly IEventProducer _eventProducer;
    private readonly ITwoFactorRepository _twoFactorRepository;
    private readonly ITwoFactorVerifier _twoFactorVerifier;
    private readonly IGuidFactory _guidFactory;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly AuthenticationConfiguration _config;
    private readonly TwoFactorConfiguration _twoFactorConfig;

    public AuthenticationService(
        ISecurityManager securityManager,
        ISymmetricCryptoService cryptoService,
        IAuthenticationRepository repository,
        ISessionFactory sessionFactory,
        IDateTimeProvider dateTimeProvider,
        ILoginAttemptTracker loginAttemptTracker,
        ISecurityAuditRepository auditRepository,
        IEventProducer eventProducer,
        ITwoFactorRepository twoFactorRepository,
        ITwoFactorVerifier twoFactorVerifier,
        IGuidFactory guidFactory,
        ILogger<AuthenticationService> logger,
        IOptions<AuthenticationConfiguration> authConfig,
        IOptions<TwoFactorConfiguration> twoFactorConfig)
    {
        _securityManager = securityManager;
        _cryptoService = cryptoService;
        _repository = repository;
        _sessionFactory = sessionFactory;
        _dateTimeProvider = dateTimeProvider;
        _loginAttemptTracker = loginAttemptTracker;
        _auditRepository = auditRepository;
        _eventProducer = eventProducer;
        _twoFactorRepository = twoFactorRepository;
        _twoFactorVerifier = twoFactorVerifier;
        _guidFactory = guidFactory;
        _logger = logger;
        _config = authConfig.Value;
        _twoFactorConfig = twoFactorConfig.Value;
    }

    /// <inheritdoc />
    public async Task<IIdentity> Authenticate(string email, string password, bool rememberMe = true,
        SessionContext? context = null, CancellationToken cancellationToken = default)
    {
        // Before throttling: a pending registration has no password to brute-force.
        if (await _repository.IsPendingRegistration(email))
        {
            _logger.LogInformation("Login failed: pending registration");
            return Identity.Fail(AuthenticationError.PendingRegistration);
        }

        // Counted per account and address: see LoginAttemptOrigin.
        var origin = new LoginAttemptOrigin(email, context?.IpAddress);
        if (await _loginAttemptTracker.IsAccountLocked(origin))
        {
            return await RefuseWhileLocked(origin, email, context);
        }

        // Progressive delay for bot protection. Cancellable: a dropped request would
        // hold its connection for the whole delay, and the wait precedes the count.
        var delaySeconds = await _loginAttemptTracker.GetDelayForUser(origin);
        if (delaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
        }

        var (userFound, user) = await _repository.TryFindUserByEmail(email);
        ApplyActiveBans(user);

        // One Argon2id before anything branches, so every refusal below costs the
        // same; the system actor, which has no password, is hashed against the decoy.
        var (salt, hash) = userFound && user!.Role != UserRole.System
            ? (user.Salt, user.PasswordHash)
            : (DecoyCredentials.Salt, DecoyCredentials.Hash);
        var passwordMatches = _securityManager.ComparePasswords(password, salt, hash);

        switch (userFound)
        {
            case false:
                await _loginAttemptTracker.RecordFailedAttempt(origin);
                _logger.LogWarning("Login failed: user not found");
                return Identity.Fail(AuthenticationError.WrongLogin);
            // The system actor is refused by its role, exactly like a wrong password.
            case true when user!.Role == UserRole.System || !passwordMatches:
                await _loginAttemptTracker.RecordFailedAttempt(origin);

                // Only the attempt that crosses the threshold reaches this while
                // locked, so the owner hears about each lockout once.
                if (await _loginAttemptTracker.IsAccountLocked(origin))
                {
                    await _eventProducer.SendAsync(EventType.AccountLocked, user.UserId);
                }

                await _auditRepository.LogAsync(user.UserId, SecurityEventType.LoginFailure,
                    context?.IpAddress, context?.UserAgent, "Wrong password");
                return Identity.Fail(AuthenticationError.WrongPassword);

            // Only past the proven password may the answer say what moderation did
            // with the account (SECURITY.md), and the journal records the refusal.
            case true when RefuseClosed(user) is { } closed:
                await _auditRepository.LogAsync(user.UserId, SecurityEventType.LoginFailure,
                    context?.IpAddress, context?.UserAgent, closed.Reason);
                return closed.Refusal;

            default:
                // Nothing about a login waiting for its second factor is recorded yet.
                if (await _twoFactorRepository.IsConfirmed(user.UserId, cancellationToken))
                {
                    return await OweSecondFactor(user, email, rememberMe, context, cancellationToken);
                }

                return await SignIn(user, email, persistent: rememberMe, context);
        }
    }

    /// <inheritdoc />
    public async Task<IIdentity> CompleteSecondFactor(
        Guid challengeId, string code, SessionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var challenge = await _twoFactorRepository.FindChallenge(challengeId, cancellationToken);
        if (challenge == null)
        {
            _logger.LogWarning("Second factor failed: unknown challenge");
            return Identity.Fail(AuthenticationError.TwoFactorRejected);
        }

        // The counter of the password step, keyed on the same pair.
        var origin = new LoginAttemptOrigin(challenge.Account, context?.IpAddress);

        if (challenge.ExpiresUtc <= _dateTimeProvider.Now)
        {
            await _twoFactorRepository.RemoveChallenge(challengeId, cancellationToken);
            _logger.LogWarning("Second factor failed: challenge expired. UserId={UserId}", challenge.UserId);
            return Identity.Fail(AuthenticationError.TwoFactorRejected);
        }

        if (await _loginAttemptTracker.IsAccountLocked(origin))
        {
            _logger.LogWarning("Second factor refused: account locked. UserId={UserId}", challenge.UserId);
            return Identity.Fail(AuthenticationError.TwoFactorRejected);
        }

        var state = await _twoFactorRepository.Find(challenge.UserId, cancellationToken);
        if (state is not { ConfirmedUtc: not null })
        {
            // The factor went away between the two steps. Nothing to prove, and
            // nothing to hand out either: the login starts again.
            await _twoFactorRepository.RemoveChallenge(challengeId, cancellationToken);
            return Identity.Fail(AuthenticationError.TwoFactorRejected);
        }

        if (!await _twoFactorVerifier.Accept(state, code, context?.IpAddress, cancellationToken))
        {
            return await RefuseWrongCode(challenge, origin, context, cancellationToken);
        }

        // Spent whichever way it ends, and spent before anything is handed out.
        await _twoFactorRepository.RemoveChallenge(challengeId, cancellationToken);

        // Asked again: a ban issued between the two steps has to take effect.
        var user = await _repository.FindUser(challenge.UserId);
        ApplyActiveBans(user);

        if (user == null)
        {
            return Identity.Fail(AuthenticationError.SessionExpired);
        }

        if (RefuseClosed(user) is { } closed)
        {
            await _auditRepository.LogAsync(user.UserId, SecurityEventType.LoginFailure,
                context?.IpAddress, context?.UserAgent, closed.Reason);
            return closed.Refusal;
        }

        // A sign-in that passed the factor is the owner saying "the device is
        // still mine", which is the whole reason a scheduled removal can be
        // called off without touching a mailbox.
        if (await _twoFactorRepository.CancelScheduledRemoval(user.UserId, cancellationToken))
        {
            await _auditRepository.LogAsync(user.UserId, SecurityEventType.TwoFactorRemovalCancelled,
                context?.IpAddress, context?.UserAgent);
        }

        // "Remember me" as it was ticked on the first step: the second step has
        // no checkbox and must not quietly answer for the person.
        return await SignIn(user, challenge.Account, persistent: challenge.Persistent, context);
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

        // Sequential: one DbContext refuses parallel queries inside one scope.
        var user = await _repository.FindUser(userId);
        var session = await _repository.FindUserSession(userId, sessionId);
        var settings = await _repository.FindUserSettings(userId);
        ApplyActiveBans(user);

        if (user == null)
        {
            _logger.LogWarning("Token authentication failed: user not found. UserId={UserId}", userId);
            return Identity.Fail(AuthenticationError.SessionExpired);
        }

        if (RefuseClosed(user) is { } closed)
        {
            _logger.LogWarning("Token authentication failed: {Reason}. UserId={UserId}", closed.Reason, userId);
            return closed.Refusal;
        }

        if (session == null)
        {
            return Identity.Fail(AuthenticationError.SessionExpired);
        }

        if (session.ExpirationUtc < _dateTimeProvider.Now)
        {
            await _repository.RemoveSession(userId, sessionId);
            return Identity.Fail(AuthenticationError.SessionExpired);
        }

        // Sliding window with a ceiling: the lifetime the session was issued with,
        // counted from issue, so a session has a last day.
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

        // A session without a password still passes the account-state gates.
        ApplyActiveBans(user);

        if (RefuseClosed(user) is { } closed)
        {
            _logger.LogWarning("Direct authentication failed: {Reason}. UserId={UserId}", closed.Reason, userId);
            return closed.Refusal;
        }

        // A session minted without a password is what a second factor prevents.
        if (await _twoFactorRepository.IsConfirmed(userId))
        {
            _logger.LogWarning(
                "Direct authentication refused: account has a second factor. UserId={UserId}", userId);
            return Identity.Fail(AuthenticationError.Forbidden);
        }

        // An ordinary session, with its address, agent, activity stamp and journal entry.
        await _repository.UpdateActivity(userId, _dateTimeProvider.Now);
        var session = _sessionFactory.Create(persistent: false, context: context);
        var settings = await _repository.FindUserSettings(userId);

        await _auditRepository.LogAsync(userId, SecurityEventType.LoginSuccess,
            context?.IpAddress, context?.UserAgent);

        return await CreateAuthenticationResult(user, session, settings);
    }

    /// <summary>
    /// The refusal a locked account answers with, and the owner's journal entry
    /// for it when the address belongs to somebody.
    /// </summary>
    private async Task<IIdentity> RefuseWhileLocked(
        LoginAttemptOrigin origin, string email, SessionContext? context)
    {
        var remainingSeconds = await _loginAttemptTracker.GetRemainingLockoutSeconds(origin);
        _logger.LogWarning("Login failed: account locked due to too many failed attempts. RemainingSeconds={RemainingSeconds}",
            remainingSeconds);

        var (_, lockedUser) = await _repository.TryFindUserByEmail(email);
        if (lockedUser != null)
        {
            await _auditRepository.LogAsync(lockedUser.UserId, SecurityEventType.AccountLocked,
                context?.IpAddress, context?.UserAgent, "Account locked due to failed attempts");
        }

        return Identity.Fail(AuthenticationError.AccountLocked, remainingSeconds);
    }

    /// <summary>
    /// The password is proven and a second factor is owed: a challenge for the
    /// second step instead of a session.
    /// </summary>
    private async Task<IIdentity> OweSecondFactor(
        AuthenticatedUser user, string email, bool rememberMe, SessionContext? context,
        CancellationToken cancellationToken)
    {
        var challenge = new TwoFactorChallengeState
        {
            ChallengeId = _guidFactory.Create(),
            UserId = user.UserId,
            // The identifier the password step counted under, so a
            // wrong code lands in the same counter as a wrong
            // password instead of one of its own.
            Account = email,
            CreatedUtc = _dateTimeProvider.Now,
            ExpiresUtc = _dateTimeProvider.Now +
                         TimeSpan.FromMinutes(_twoFactorConfig.ChallengeLifetimeMinutes),
            Persistent = rememberMe,
            IpAddress = context?.IpAddress,
            UserAgent = context?.UserAgent
        };
        await _twoFactorRepository.AddChallenge(challenge, cancellationToken);

        _logger.LogInformation(
            "Password accepted, second factor owed. UserId={UserId}", user.UserId);
        return Identity.SecondFactorRequired(challenge.ChallengeId);
    }

    /// <summary>
    /// The refusal a wrong code answers with: counted under the counter of the
    /// password step, and the challenge destroyed once it is out of attempts.
    /// </summary>
    private async Task<IIdentity> RefuseWrongCode(
        TwoFactorChallengeState challenge, LoginAttemptOrigin origin, SessionContext? context,
        CancellationToken cancellationToken)
    {
        await _loginAttemptTracker.RecordFailedAttempt(origin);
        var attempts = await _twoFactorRepository.CountChallengeAttempt(challenge.ChallengeId, cancellationToken);
        if (attempts >= _twoFactorConfig.ChallengeAttemptLimit)
        {
            // The challenge is destroyed rather than left counting: carrying
            // on costs the password again, which is the price of guessing.
            await _twoFactorRepository.RemoveChallenge(challenge.ChallengeId, cancellationToken);
        }

        await _auditRepository.LogAsync(challenge.UserId, SecurityEventType.LoginFailure,
            context?.IpAddress, context?.UserAgent, "Двухфакторная аутентификация");
        return Identity.Fail(AuthenticationError.TwoFactorRejected);
    }

    /// <summary>
    /// The tail every proven login shares, the password step and the second
    /// factor alike: the attempt counter reset, the activity stamp, the session,
    /// the settings and the journal entry, in that order.
    /// </summary>
    /// <param name="user">The account the login is for</param>
    /// <param name="account">The identifier the attempts were counted under</param>
    /// <param name="persistent">"Remember me" as it was ticked on the password step</param>
    /// <param name="context">Address and agent the login came from</param>
    private async Task<IIdentity> SignIn(
        AuthenticatedUser user, string account, bool persistent, SessionContext? context)
    {
        await _loginAttemptTracker.ResetAttempts(account);
        await _repository.UpdateActivity(user.UserId, _dateTimeProvider.Now);

        var session = _sessionFactory.Create(persistent: persistent, context: context);
        var settings = await _repository.FindUserSettings(user.UserId);

        // Awaited: the suspicious-login check that follows drops the newest entry
        // of the trail as "this login".
        await _auditRepository.LogAsync(user.UserId, SecurityEventType.LoginSuccess,
            context?.IpAddress, context?.UserAgent);

        return await CreateAuthenticationResult(user, session, settings);
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
    /// Nothing writes the user's own AccessPolicy column, and folded where the
    /// identity is built a ban starts and stops on the next request by itself.
    /// </remarks>
    private void ApplyActiveBans(AuthenticatedUser? user)
    {
        if (user == null)
        {
            return;
        }

        user.AccessPolicy = user.EffectiveAccessPolicyAt(_dateTimeProvider.Now);
    }

    /// <summary>
    /// The refusal of an account moderation has removed or fully banned, with the
    /// reason the journal records it under, or null for an open account.
    /// </summary>
    private (IIdentity Refusal, string Reason)? RefuseClosed(AuthenticatedUser user)
    {
        if (user.IsRemoved)
        {
            return (Identity.Fail(AuthenticationError.Removed), "Account removed");
        }

        if (user.AccessPolicy.HasFlag(AccessPolicy.FullBan))
        {
            return (Identity.Banned(user.BanEndsAt(_dateTimeProvider.Now)), "Account banned");
        }

        return null;
    }
}
