using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.TwoFactor;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Authentication;

public class AuthenticationServiceShould : UnitTestBase
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
    private readonly ITwoFactorRepository _twoFactorRepository;
    private readonly ITwoFactorVerifier _twoFactorVerifier;
    private readonly IGuidFactory _guidFactory;
    private readonly AuthenticationService _service;

    private static readonly Guid _userId = Guid.Parse("7b1c2d3e-4f50-4a61-8b72-9c83d4e5f607");
    private static readonly Guid _sessionId = Guid.Parse("1a2b3c4d-5e6f-4071-8293-a4b5c6d7e8f9");

    public AuthenticationServiceShould()
    {
        _securityManager = Mock<ISecurityManager>();
        _cryptoService = Mock<ISymmetricCryptoService>();
        _repository = Mock<IAuthenticationRepository>();
        _sessionFactory = Mock<ISessionFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _identityProvider = Mock<IIdentityProvider>();
        _loginAttemptTracker = Mock<ILoginAttemptTracker>();
        _auditService = Mock<ISecurityAuditRepository>();
        _eventProducer = Mock<IEventProducer>();
        _twoFactorRepository = Mock<ITwoFactorRepository>();
        _twoFactorVerifier = Mock<ITwoFactorVerifier>();
        _guidFactory = Mock<IGuidFactory>();
        var logger = Mock<ILogger<AuthenticationService>>();
        var config = Options.Create(new AuthenticationConfiguration
        {
            SessionRefreshMinutes = 60,
            ActivityTrackingMinutes = 5,
            SessionExpirationHours = 24,
            PersistentSessionExpirationDays = 365
        });

        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _service = new AuthenticationService(
            _securityManager,
            _cryptoService,
            _repository,
            _sessionFactory,
            _dateTimeProvider,
            _identityProvider,
            _loginAttemptTracker,
            _auditService,
            _eventProducer,
            _twoFactorRepository,
            _twoFactorVerifier,
            _guidFactory,
            logger,
            config,
            Options.Create(new TwoFactorConfiguration()));
    }

    [Fact]
    public async Task ReturnFailureWhenEmailIsPendingRegistration()
    {
        var email = "test@example.com";
        _repository.IsPendingRegistration(email).Returns(true);

        var result = await _service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.PendingRegistration);
    }

    [Fact]
    public async Task ReturnFailureWhenAccountIsLocked()
    {
        var email = "test@example.com";
        _repository.IsPendingRegistration(email).Returns(false);
        _loginAttemptTracker.IsAccountLocked(new LoginAttemptOrigin(email, null)).Returns(true);
        _loginAttemptTracker.GetRemainingLockoutSeconds(new LoginAttemptOrigin(email, null)).Returns(300);

        var result = await _service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.AccountLocked);
    }

    [Fact]
    public async Task ReturnFailureWhenUserNotFound()
    {
        var email = "test@example.com";
        _repository.IsPendingRegistration(email).Returns(false);
        _loginAttemptTracker.IsAccountLocked(new LoginAttemptOrigin(email, null)).Returns(false);
        _loginAttemptTracker.GetDelayForUser(new LoginAttemptOrigin(email, null)).Returns(0);
        _repository.TryFindUserByEmail(email).Returns(((bool, AuthenticatedUser?))(false, null));

        var result = await _service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.WrongLogin);
        await _loginAttemptTracker.Received(1).RecordFailedAttempt(new LoginAttemptOrigin(email, null));
        // Answered in the same time as a wrong password, not only in the same
        // words: with no hash of anything, a missing account comes back before
        // Argon2id would have finished, and that difference is the answer
        _securityManager.Received(1).ComparePasswords(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ReturnFailureWhenPasswordIsWrong()
    {
        var email = "test@example.com";
        var user = new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Email = email,
            Salt = "salt",
            PasswordHash = "hash",
            IsRemoved = false,
            AccessPolicy = AccessPolicy.NotSpecified
        };

        _repository.IsPendingRegistration(email).Returns(false);
        _loginAttemptTracker.IsAccountLocked(new LoginAttemptOrigin(email, null)).Returns(false);
        _loginAttemptTracker.GetDelayForUser(new LoginAttemptOrigin(email, null)).Returns(0);
        _repository.TryFindUserByEmail(email).Returns((true, user));
        _securityManager.ComparePasswords("wrongpassword", user.Salt, user.PasswordHash).Returns(false);

        var result = await _service.Authenticate(email, "wrongpassword");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.WrongPassword);
        await _loginAttemptTracker.Received(1).RecordFailedAttempt(new LoginAttemptOrigin(email, null));
        await _auditService.Received(1).LogAsync(user.UserId, SecurityEventType.LoginFailure,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task AnnounceTheLockoutOnTheAttemptThatCausesIt()
    {
        var email = "test@example.com";
        var user = new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Email = email,
            Salt = "salt",
            PasswordHash = "hash",
            IsRemoved = false,
            AccessPolicy = AccessPolicy.NotSpecified
        };
        var origin = new LoginAttemptOrigin(email, null);

        _repository.IsPendingRegistration(email).Returns(false);
        _repository.TryFindUserByEmail(email).Returns((true, user));
        _loginAttemptTracker.GetDelayForUser(origin).Returns(0);
        _securityManager.ComparePasswords("wrongpassword", user.Salt, user.PasswordHash).Returns(false);

        // Unlocked when the attempt starts, locked once it has been counted:
        // that is the one attempt the notification belongs to.
        _loginAttemptTracker.IsAccountLocked(origin).Returns(false, true);

        await _service.Authenticate(email, "wrongpassword");

        await _eventProducer.Received(1).SendAsync(EventType.AccountLocked, user.UserId);
    }

    [Fact]
    public async Task AuthenticateSuccessfullyWithValidCredentials()
    {
        var email = "test@example.com";
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var user = new AuthenticatedUser
        {
            UserId = userId,
            Email = email,
            Username = "testuser",
            Role = UserRole.RegularUser,
            Salt = "salt",
            PasswordHash = "hash",
            IsRemoved = false,
            AccessPolicy = AccessPolicy.NotSpecified
        };
        var createSession = new CreateSession { Id = sessionId };
        var session = new Session { Id = sessionId };
        var settings = UserSettings.Default;

        _repository.IsPendingRegistration(email).Returns(false);
        _loginAttemptTracker.IsAccountLocked(new LoginAttemptOrigin(email, null)).Returns(false);
        _loginAttemptTracker.GetDelayForUser(new LoginAttemptOrigin(email, null)).Returns(0);
        _repository.TryFindUserByEmail(email).Returns((true, user));
        _securityManager.ComparePasswords("password", user.Salt, user.PasswordHash).Returns(true);
        _sessionFactory.Create(true, null).Returns(createSession);
        _repository.FindUserSettings(userId).Returns(settings);
        _repository.AddSession(userId, createSession).Returns(session);
        _cryptoService.Encrypt(Arg.Any<string>()).Returns("encrypted-token");

        var result = await _service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeTrue();
        result.User.UserId.Should().Be(userId);
        await _loginAttemptTracker.Received(1).ResetAttempts(email);
        await _repository.Received(1).UpdateActivity(userId, Arg.Any<DateTimeOffset>());
        await _auditService.Received(1).LogAsync(userId, SecurityEventType.LoginSuccess,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task RefuseLoginForFullyBannedUser()
    {
        var email = "banned@example.com";
        var user = Create.User()
            .WithRole(UserRole.RegularUser)
            .WithAccessPolicy(AccessPolicy.FullBan)
            .WithCredentials("salt", "hash")
            .Please();

        _repository.IsPendingRegistration(email).Returns(false);
        _loginAttemptTracker.IsAccountLocked(new LoginAttemptOrigin(email, null)).Returns(false);
        _loginAttemptTracker.GetDelayForUser(new LoginAttemptOrigin(email, null)).Returns(0);
        _repository.TryFindUserByEmail(email).Returns((true, user));
        // The password is right, and that is what earns the real reason: to
        // everyone else a banned account answers like a wrong password, see
        // HideTheStateOfTheAccountUntilThePasswordIsProven
        _securityManager.ComparePasswords("password", user.Salt, user.PasswordHash).Returns(true);

        var result = await _service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.Banned);
        // No session is minted for a banned account
        await _repository.DidNotReceive().AddSession(Arg.Any<Guid>(), Arg.Any<CreateSession>());
    }

    /// <summary>
    /// A wrong password answers the same whatever state the account is in.
    /// </summary>
    /// <remarks>
    /// "Removed" and "banned" used to be decided before the password was
    /// compared, so anyone who typed an address learned what moderation had done
    /// to the person behind it, with no password and with nothing counted
    /// against the asking. That an address is registered is disclosed here by a
    /// recorded decision (SECURITY.md, the enumeration exception); what was done
    /// with the account is not.
    /// </remarks>
    [Theory]
    [InlineData(true, AccessPolicy.NotSpecified)]
    [InlineData(false, AccessPolicy.FullBan)]
    public async Task HideTheStateOfTheAccountUntilThePasswordIsProven(
        bool isRemoved, AccessPolicy accessPolicy)
    {
        var email = "test@example.com";
        var user = Create.User()
            .WithRole(UserRole.RegularUser)
            .WithAccessPolicy(accessPolicy)
            .WithCredentials("salt", "hash")
            .Please();
        user.IsRemoved = isRemoved;
        var origin = new LoginAttemptOrigin(email, null);

        _repository.IsPendingRegistration(email).Returns(false);
        _loginAttemptTracker.IsAccountLocked(origin).Returns(false);
        _loginAttemptTracker.GetDelayForUser(origin).Returns(0);
        _repository.TryFindUserByEmail(email).Returns((true, user));
        _securityManager.ComparePasswords("wrongpassword", user.Salt, user.PasswordHash).Returns(false);

        var result = await _service.Authenticate(email, "wrongpassword");

        result.Error.Should().Be(AuthenticationError.WrongPassword);
        // And it costs the guess an attempt, like any other wrong password does
        await _loginAttemptTracker.Received(1).RecordFailedAttempt(origin);
    }

    [Fact]
    public async Task RefuseLoginForARemovedAccountOnceThePasswordIsProven()
    {
        var email = "removed@example.com";
        var user = Create.User()
            .WithRole(UserRole.RegularUser)
            .WithCredentials("salt", "hash")
            .Please();
        user.IsRemoved = true;

        _repository.IsPendingRegistration(email).Returns(false);
        _loginAttemptTracker.IsAccountLocked(new LoginAttemptOrigin(email, null)).Returns(false);
        _loginAttemptTracker.GetDelayForUser(new LoginAttemptOrigin(email, null)).Returns(0);
        _repository.TryFindUserByEmail(email).Returns((true, user));
        _securityManager.ComparePasswords("password", user.Salt, user.PasswordHash).Returns(true);

        var result = await _service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.Removed);
        await _repository.DidNotReceive().AddSession(Arg.Any<Guid>(), Arg.Any<CreateSession>());
    }

    [Fact]
    public async Task RefuseLoginForTheSystemActorEvenWhenItsPasswordMatches()
    {
        var email = "system@dm.local";
        var user = Create.User()
            .WithRole(UserRole.System)
            .WithCredentials("salt", "hash")
            .Please();

        _repository.IsPendingRegistration(email).Returns(false);
        _loginAttemptTracker.IsAccountLocked(new LoginAttemptOrigin(email, null)).Returns(false);
        _loginAttemptTracker.GetDelayForUser(new LoginAttemptOrigin(email, null)).Returns(0);
        _repository.TryFindUserByEmail(email).Returns((true, user));
        // Everything a successful login needs is stubbed, a matching password
        // included: what stops the robot is the check on its role, and the refusal
        // has to hold with the credentials of a real account behind it.
        //
        // Any arguments, because the actor is deliberately hashed against the
        // decoy rather than against its own columns - the seed leaves those empty,
        // and the hash exists to spend the time, not to ask a question.
        _securityManager
            .ComparePasswords(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _sessionFactory.Create(true, null).Returns(new CreateSession { Id = Guid.NewGuid() });
        _repository.FindUserSettings(user.UserId).Returns(UserSettings.Default);
        _repository.AddSession(user.UserId, Arg.Any<CreateSession>()).Returns(new Session { Id = Guid.NewGuid() });
        _cryptoService.Encrypt(Arg.Any<string>()).Returns("encrypted-token");

        var result = await _service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        // Answered as a wrong password on purpose: a distinct error would point at
        // the one account that exists but can never be logged into
        result.Error.Should().Be(AuthenticationError.WrongPassword);
        await _repository.DidNotReceive().AddSession(Arg.Any<Guid>(), Arg.Any<CreateSession>());

        // And it pays for the refusal like every other refusal does. Short-circuited
        // on the role, this answer came back some hundred and fifty milliseconds
        // before any other and named the robot's address by that alone.
        _securityManager.Received(1).ComparePasswords(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RefuseTokenOfFullyBannedUser()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var user = Create.User(userId)
            .WithRole(UserRole.RegularUser)
            .WithAccessPolicy(AccessPolicy.FullBan)
            .Please();

        _cryptoService.Decrypt("token").Returns($"{{\"userId\":\"{userId}\",\"sessionId\":\"{sessionId}\"}}");
        _repository.FindUser(userId).Returns(user);
        _repository.FindUserSession(userId, sessionId)
            .Returns(new Session { Id = sessionId, ExpirationUtc = DateTime.UtcNow.AddDays(1) });
        _repository.FindUserSettings(userId).Returns(UserSettings.Default);

        var result = await _service.Authenticate("token");

        // A ban applied after the cookie was issued must take effect on the very
        // next request, not at session expiry
        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.Banned);
    }

    [Fact]
    public async Task LookUpTheSessionUnderTheUserFromTheToken()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var user = Create.User(userId).WithRole(UserRole.RegularUser).Please();

        _cryptoService.Decrypt("token").Returns($"{{\"userId\":\"{userId}\",\"sessionId\":\"{sessionId}\"}}");
        _repository.FindUser(userId).Returns(user);
        _repository.FindUserSettings(userId).Returns(UserSettings.Default);
        _repository.FindUserSession(userId, sessionId)
            .Returns(new Session { Id = sessionId, ExpirationUtc = DateTime.UtcNow.AddDays(1) });

        var result = await _service.Authenticate("token");

        result.User.IsAuthenticated.Should().BeTrue();
        // Both halves of the token are used together. Looking the session up by
        // its id alone would authenticate a token whose userId and sessionId
        // belong to different people.
        await _repository.Received(1).FindUserSession(userId, sessionId);
    }

    [Fact]
    public async Task RefuseTokenWhoseSessionBelongsToSomebodyElse()
    {
        var userId = Guid.NewGuid();
        var foreignSessionId = Guid.NewGuid();
        var user = Create.User(userId).WithRole(UserRole.RegularUser).Please();

        _cryptoService.Decrypt("token").Returns($"{{\"userId\":\"{userId}\",\"sessionId\":\"{foreignSessionId}\"}}");
        _repository.FindUser(userId).Returns(user);
        _repository.FindUserSettings(userId).Returns(UserSettings.Default);
        // The session exists in the store, but not under this user
        _repository.FindUserSession(userId, foreignSessionId).Returns((Session?)null);

        var result = await _service.Authenticate("token");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.SessionExpired);
    }

    [Fact]
    public async Task ApplyABanRowToTheIdentityItBuilds()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        _dateTimeProvider.Now.Returns(now);

        var user = Create.User(userId).WithRole(UserRole.RegularUser).Please();
        // The ban lives in its own table and reaches the identity as a restriction
        // with a window; the user's own column stays NotSpecified forever
        user.AccessRestrictions = [
            new AccessRestriction(AccessPolicy.DemocraticBan, now.AddDays(-1), now.AddDays(1))
        ];

        _cryptoService.Decrypt("token").Returns($"{{\"userId\":\"{userId}\",\"sessionId\":\"{sessionId}\"}}");
        _repository.FindUser(userId).Returns(user);
        _repository.FindUserSettings(userId).Returns(UserSettings.Default);
        _repository.FindUserSession(userId, sessionId)
            .Returns(new Session { Id = sessionId, ExpirationUtc = now.AddDays(1).UtcDateTime });

        var result = await _service.Authenticate("token");

        result.User.IsAuthenticated.Should().BeTrue();
        // Every authorization resolver reads AccessPolicy. Without the fold the
        // ban would be invisible to all of them.
        result.User.AccessPolicy.Should().HaveFlag(AccessPolicy.DemocraticBan);
    }

    [Theory]
    [InlineData(-10, -5)] // ban already served
    [InlineData(5, 10)]   // ban scheduled but not started
    public async Task IgnoreABanThatIsNotInForce(int startsInDays, int endsInDays)
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        _dateTimeProvider.Now.Returns(now);

        var user = Create.User(userId).WithRole(UserRole.RegularUser).Please();
        user.AccessRestrictions = [
            new AccessRestriction(AccessPolicy.DemocraticBan, now.AddDays(startsInDays), now.AddDays(endsInDays))
        ];

        _cryptoService.Decrypt("token").Returns($"{{\"userId\":\"{userId}\",\"sessionId\":\"{sessionId}\"}}");
        _repository.FindUser(userId).Returns(user);
        _repository.FindUserSettings(userId).Returns(UserSettings.Default);
        _repository.FindUserSession(userId, sessionId)
            .Returns(new Session { Id = sessionId, ExpirationUtc = now.AddDays(1).UtcDateTime });

        var result = await _service.Authenticate("token");

        // The row stays for the moderation history; it must stop restricting the
        // moment it stops being in force, with no job to run
        result.User.AccessPolicy.Should().Be(AccessPolicy.NotSpecified);
    }

    [Fact]
    public async Task RefuseLoginWhenTheFullBanComesFromABanRow()
    {
        var email = "banned@example.com";
        var now = DateTimeOffset.UtcNow;
        _dateTimeProvider.Now.Returns(now);

        var user = Create.User()
            .WithRole(UserRole.RegularUser)
            .WithCredentials("salt", "hash")
            .Please();
        user.AccessRestrictions = [
            new AccessRestriction(AccessPolicy.FullBan, now.AddHours(-1), now.AddHours(1))
        ];

        _repository.IsPendingRegistration(email).Returns(false);
        _loginAttemptTracker.IsAccountLocked(new LoginAttemptOrigin(email, null)).Returns(false);
        _loginAttemptTracker.GetDelayForUser(new LoginAttemptOrigin(email, null)).Returns(0);
        _repository.TryFindUserByEmail(email).Returns((true, user));
        _securityManager.ComparePasswords("password", user.Salt, user.PasswordHash).Returns(true);

        var result = await _service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        // A ban that comes from a row refuses exactly like the flag does
        result.Error.Should().Be(AuthenticationError.Banned);
    }

    [Fact]
    public async Task LogoutAndRemoveSession()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = userId, Role = UserRole.RegularUser },
            new Session { Id = sessionId },
            UserSettings.Default,
            "token");

        _identityProvider.Current.Returns(identity);

        var result = await _service.Logout();

        result.User.IsAuthenticated.Should().BeFalse();
        await _repository.Received(1).RemoveSession(userId, sessionId);
        await _repository.Received(1).UpdateActivity(userId, Arg.Any<DateTimeOffset>());
        await _auditService.Received(1).LogAsync(userId, SecurityEventType.Logout, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task LogoutElsewhereButKeepCurrentSession()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = userId, Role = UserRole.RegularUser },
            new Session { Id = sessionId },
            UserSettings.Default,
            "token");

        _identityProvider.Current.Returns(identity);

        var result = await _service.LogoutElsewhere();

        result.Should().Be(identity);
        await _repository.Received(1).RemoveSessionsExcept(userId, sessionId);
        await _auditService.Received(1).LogAsync(userId, SecurityEventType.LogoutElsewhere, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task TerminateSessionForCurrentUser()
    {
        var userId = Guid.NewGuid();
        var currentSessionId = Guid.NewGuid();
        var targetSessionId = Guid.NewGuid();
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = userId, Role = UserRole.RegularUser },
            new Session { Id = currentSessionId },
            UserSettings.Default,
            "token");

        _identityProvider.Current.Returns(identity);

        await _service.TerminateSession(userId, targetSessionId);

        await _repository.Received(1).RemoveSession(userId, targetSessionId);
        await _auditService.Received(1).LogAsync(userId, SecurityEventType.SessionTerminated,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task ThrowWhenTerminatingOtherUserSession()
    {
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = currentUserId },
            new Session { Id = Guid.NewGuid() },
            UserSettings.Default,
            "token");

        _identityProvider.Current.Returns(identity);

        // Not the caller's session to end, and a refusal is a refusal: the error
        // middleware maps HttpException and its kin and nothing else, so anything
        // outside that family reaches the client as a server fault.
        var thrown = await Assert.ThrowsAsync<HttpException>(
            () => _service.TerminateSession(otherUserId, sessionId));
        thrown.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
    /// <summary>
    /// A session has a last day, and refreshing it does not move that day.
    /// </summary>
    /// <remarks>
    /// The sliding window extended the session from its own expiry, so a token used
    /// once a week never expired at all: the account it belongs to could be renamed,
    /// have its password changed and be forgotten, and the token would still open a
    /// session. The ceiling is the lifetime the session was issued with, counted from
    /// issue - which is a number the configuration already states rather than a third
    /// one nobody chose.
    /// </remarks>
    [Fact]
    public async Task RefreshASessionNoFurtherThanTheDayItWasIssuedFor()
    {
        var now = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        _dateTimeProvider.Now.Returns(now);

        // Issued 23 hours ago against a 24 hour lifetime and inside the refresh
        // window: an unbounded slide would push it half an hour past the ceiling.
        var session = new Session
        {
            Id = _sessionId,
            CreatedUtc = now.AddHours(-23),
            ExpirationUtc = now.AddMinutes(30),
            Persistent = false,
        };

        await AuthenticateWith(session);

        // The ceiling is the lifetime the session was issued with, counted from issue.
        await _repository.Received(1).RefreshSession(
            _userId, _sessionId, session.CreatedUtc.AddHours(24));
    }

    [Fact]
    public async Task StopRefreshingASessionThatHasReachedItsCeiling()
    {
        var now = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        _dateTimeProvider.Now.Returns(now);

        // Already at the ceiling: every request of the last window would otherwise
        // write the same value again.
        var session = new Session
        {
            Id = _sessionId,
            CreatedUtc = now.AddHours(-23),
            ExpirationUtc = now.AddHours(-23).AddHours(24),
            Persistent = false,
        };

        await AuthenticateWith(session);

        await _repository.DidNotReceive().RefreshSession(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>());
    }

    /// <summary>Runs token authentication against a session and nothing else.</summary>
    private async Task AuthenticateWith(Session session)
    {
        var user = new AuthenticatedUser
        {
            UserId = _userId,
            Username = "reader",
            Role = UserRole.RegularUser,
            LastActivityUtc = session.CreatedUtc,
        };

        _repository.FindUser(_userId).Returns(user);
        _repository.FindUserSession(_userId, _sessionId).Returns(session);
        _repository.FindUserSettings(_userId).Returns(UserSettings.Default);
        _cryptoService
            .Decrypt(Arg.Any<string>()).Returns(
                "{\"userId\":\"" + _userId + "\",\"sessionId\":\"" + _sessionId + "\"}");

        await _service.Authenticate("token");
    }
}
