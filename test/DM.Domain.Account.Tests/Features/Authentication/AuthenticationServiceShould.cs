using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Testing.Dsl;
using DM.Testing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Authentication;

public class AuthenticationServiceShould : UnitTestBase
{
    private readonly Mock<ISecurityManager> _securityManager;
    private readonly Mock<ISymmetricCryptoService> _cryptoService;
    private readonly Mock<IAuthenticationRepository> _repository;
    private readonly Mock<ISessionFactory> _sessionFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<ILoginAttemptTracker> _loginAttemptTracker;
    private readonly Mock<ISecurityAuditService> _auditService;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly AuthenticationService _service;

    public AuthenticationServiceShould()
    {
        _securityManager = Mock<ISecurityManager>();
        _cryptoService = Mock<ISymmetricCryptoService>();
        _repository = Mock<IAuthenticationRepository>();
        _sessionFactory = Mock<ISessionFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _identityProvider = Mock<IIdentityProvider>();
        _loginAttemptTracker = Mock<ILoginAttemptTracker>();
        _auditService = Mock<ISecurityAuditService>();
        _eventProducer = Mock<IEventProducer>();
        var logger = Mock<ILogger<AuthenticationService>>();
        var config = Options.Create(new AuthenticationConfiguration
        {
            SessionRefreshMinutes = 60,
            ActivityTrackingMinutes = 5
        });

        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _service = new AuthenticationService(
            _securityManager.Object,
            _cryptoService.Object,
            _repository.Object,
            _sessionFactory.Object,
            _dateTimeProvider.Object,
            _identityProvider.Object,
            _loginAttemptTracker.Object,
            _auditService.Object,
            _eventProducer.Object,
            logger.Object,
            config);
    }

    [Fact]
    public async Task ReturnFailureWhenEmailIsPendingRegistration()
    {
        var email = "test@example.com";
        _repository.Setup(r => r.IsPendingRegistration(email)).ReturnsAsync(true);

        var result = await _service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.PendingRegistration);
    }

    [Fact]
    public async Task ReturnFailureWhenAccountIsLocked()
    {
        var email = "test@example.com";
        _repository.Setup(r => r.IsPendingRegistration(email)).ReturnsAsync(false);
        _loginAttemptTracker.Setup(t => t.IsAccountLocked(new LoginAttemptOrigin(email, null))).ReturnsAsync(true);
        _loginAttemptTracker.Setup(t => t.GetRemainingLockoutSeconds(new LoginAttemptOrigin(email, null))).ReturnsAsync(300);

        var result = await _service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.AccountLocked);
    }

    [Fact]
    public async Task ReturnFailureWhenUserNotFound()
    {
        var email = "test@example.com";
        _repository.Setup(r => r.IsPendingRegistration(email)).ReturnsAsync(false);
        _loginAttemptTracker.Setup(t => t.IsAccountLocked(new LoginAttemptOrigin(email, null))).ReturnsAsync(false);
        _loginAttemptTracker.Setup(t => t.GetDelayForUser(new LoginAttemptOrigin(email, null))).ReturnsAsync(0);
        _repository.Setup(r => r.TryFindUserByEmail(email)).ReturnsAsync(((bool, AuthenticatedUser?))(false, null));

        var result = await _service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.WrongLogin);
        _loginAttemptTracker.Verify(t => t.RecordFailedAttempt(new LoginAttemptOrigin(email, null)), Times.Once);
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

        _repository.Setup(r => r.IsPendingRegistration(email)).ReturnsAsync(false);
        _loginAttemptTracker.Setup(t => t.IsAccountLocked(new LoginAttemptOrigin(email, null))).ReturnsAsync(false);
        _loginAttemptTracker.Setup(t => t.GetDelayForUser(new LoginAttemptOrigin(email, null))).ReturnsAsync(0);
        _repository.Setup(r => r.TryFindUserByEmail(email)).ReturnsAsync((true, user));
        _securityManager.Setup(s => s.ComparePasswords("wrongpassword", user.Salt, user.PasswordHash))
            .Returns(false);

        var result = await _service.Authenticate(email, "wrongpassword");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.WrongPassword);
        _loginAttemptTracker.Verify(t => t.RecordFailedAttempt(new LoginAttemptOrigin(email, null)), Times.Once);
        _auditService.Verify(a => a.LogAsync(user.UserId, SecurityEventType.LoginFailure,
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
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

        _repository.Setup(r => r.IsPendingRegistration(email)).ReturnsAsync(false);
        _repository.Setup(r => r.TryFindUserByEmail(email)).ReturnsAsync((true, user));
        _loginAttemptTracker.Setup(t => t.GetDelayForUser(origin)).ReturnsAsync(0);
        _securityManager.Setup(s => s.ComparePasswords("wrongpassword", user.Salt, user.PasswordHash))
            .Returns(false);

        // Unlocked when the attempt starts, locked once it has been counted:
        // that is the one attempt the notification belongs to.
        _loginAttemptTracker.SetupSequence(t => t.IsAccountLocked(origin))
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        await _service.Authenticate(email, "wrongpassword");

        _eventProducer.Verify(p => p.SendAsync(EventType.AccountLocked, user.UserId), Times.Once);
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

        _repository.Setup(r => r.IsPendingRegistration(email)).ReturnsAsync(false);
        _loginAttemptTracker.Setup(t => t.IsAccountLocked(new LoginAttemptOrigin(email, null))).ReturnsAsync(false);
        _loginAttemptTracker.Setup(t => t.GetDelayForUser(new LoginAttemptOrigin(email, null))).ReturnsAsync(0);
        _repository.Setup(r => r.TryFindUserByEmail(email)).ReturnsAsync((true, user));
        _securityManager.Setup(s => s.ComparePasswords("password", user.Salt, user.PasswordHash))
            .Returns(true);
        _sessionFactory.Setup(f => f.Create(true, false, null)).Returns(createSession);
        _repository.Setup(r => r.FindUserSettings(userId)).ReturnsAsync(settings);
        _repository.Setup(r => r.AddSession(userId, createSession)).ReturnsAsync(session);
        _cryptoService.Setup(c => c.Encrypt(It.IsAny<string>())).ReturnsAsync("encrypted-token");

        var result = await _service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeTrue();
        result.User.UserId.Should().Be(userId);
        _loginAttemptTracker.Verify(t => t.ResetAttempts(email), Times.Once);
        _repository.Verify(r => r.UpdateActivity(userId, It.IsAny<DateTimeOffset>()), Times.Once);
        _auditService.Verify(a => a.LogAsync(userId, SecurityEventType.LoginSuccess,
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
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

        _repository.Setup(r => r.IsPendingRegistration(email)).ReturnsAsync(false);
        _loginAttemptTracker.Setup(t => t.IsAccountLocked(new LoginAttemptOrigin(email, null))).ReturnsAsync(false);
        _loginAttemptTracker.Setup(t => t.GetDelayForUser(new LoginAttemptOrigin(email, null))).ReturnsAsync(0);
        _repository.Setup(r => r.TryFindUserByEmail(email)).ReturnsAsync((true, user));
        var result = await _service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.Banned);
        // ComparePasswords is left unstubbed on purpose: the ban has to decide
        // before the password is consulted, so with the branches swapped this
        // call happens and the answer turns into WrongPassword
        _securityManager.Verify(s => s.ComparePasswords(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        // No session is minted for a banned account
        _repository.Verify(r => r.AddSession(It.IsAny<Guid>(), It.IsAny<CreateSession>()), Times.Never);
    }

    [Fact]
    public async Task RefuseLoginForTheSystemActorEvenWhenItsPasswordMatches()
    {
        var email = "system@dm.local";
        var user = Create.User()
            .WithRole(UserRole.System)
            .WithCredentials("salt", "hash")
            .Please();

        _repository.Setup(r => r.IsPendingRegistration(email)).ReturnsAsync(false);
        _loginAttemptTracker.Setup(t => t.IsAccountLocked(new LoginAttemptOrigin(email, null))).ReturnsAsync(false);
        _loginAttemptTracker.Setup(t => t.GetDelayForUser(new LoginAttemptOrigin(email, null))).ReturnsAsync(0);
        _repository.Setup(r => r.TryFindUserByEmail(email)).ReturnsAsync((true, user));
        // Everything a successful login needs is stubbed, a matching password
        // included: what stops the robot in production is the empty salt and hash
        // the seed writes, and the refusal has to hold without them
        _securityManager.Setup(s => s.ComparePasswords("password", user.Salt, user.PasswordHash))
            .Returns(true);
        _sessionFactory.Setup(f => f.Create(true, false, null))
            .Returns(new CreateSession { Id = Guid.NewGuid() });
        _repository.Setup(r => r.FindUserSettings(user.UserId)).ReturnsAsync(UserSettings.Default);
        _repository.Setup(r => r.AddSession(user.UserId, It.IsAny<CreateSession>()))
            .ReturnsAsync(new Session { Id = Guid.NewGuid() });
        _cryptoService.Setup(c => c.Encrypt(It.IsAny<string>())).ReturnsAsync("encrypted-token");

        var result = await _service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        // Answered as a wrong password on purpose: a distinct error would point at
        // the one account that exists but can never be logged into
        result.Error.Should().Be(AuthenticationError.WrongPassword);
        _repository.Verify(r => r.AddSession(It.IsAny<Guid>(), It.IsAny<CreateSession>()), Times.Never);
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

        _cryptoService.Setup(c => c.Decrypt("token"))
            .ReturnsAsync($"{{\"userId\":\"{userId}\",\"sessionId\":\"{sessionId}\"}}");
        _repository.Setup(r => r.FindUser(userId)).ReturnsAsync(user);
        _repository.Setup(r => r.FindUserSession(userId, sessionId))
            .ReturnsAsync(new Session { Id = sessionId, ExpirationUtc = DateTime.UtcNow.AddDays(1) });
        _repository.Setup(r => r.FindUserSettings(userId)).ReturnsAsync(UserSettings.Default);

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

        _cryptoService.Setup(c => c.Decrypt("token"))
            .ReturnsAsync($"{{\"userId\":\"{userId}\",\"sessionId\":\"{sessionId}\"}}");
        _repository.Setup(r => r.FindUser(userId)).ReturnsAsync(user);
        _repository.Setup(r => r.FindUserSettings(userId)).ReturnsAsync(UserSettings.Default);
        _repository.Setup(r => r.FindUserSession(userId, sessionId))
            .ReturnsAsync(new Session { Id = sessionId, ExpirationUtc = DateTime.UtcNow.AddDays(1) });

        var result = await _service.Authenticate("token");

        result.User.IsAuthenticated.Should().BeTrue();
        // Both halves of the token are used together. Looking the session up by
        // its id alone would authenticate a token whose userId and sessionId
        // belong to different people.
        _repository.Verify(r => r.FindUserSession(userId, sessionId), Times.Once);
    }

    [Fact]
    public async Task RefuseTokenWhoseSessionBelongsToSomebodyElse()
    {
        var userId = Guid.NewGuid();
        var foreignSessionId = Guid.NewGuid();
        var user = Create.User(userId).WithRole(UserRole.RegularUser).Please();

        _cryptoService.Setup(c => c.Decrypt("token"))
            .ReturnsAsync($"{{\"userId\":\"{userId}\",\"sessionId\":\"{foreignSessionId}\"}}");
        _repository.Setup(r => r.FindUser(userId)).ReturnsAsync(user);
        _repository.Setup(r => r.FindUserSettings(userId)).ReturnsAsync(UserSettings.Default);
        // The session exists in the store, but not under this user
        _repository.Setup(r => r.FindUserSession(userId, foreignSessionId))
            .ReturnsAsync((Session?)null);

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
        _dateTimeProvider.Setup(d => d.Now).Returns(now);

        var user = Create.User(userId).WithRole(UserRole.RegularUser).Please();
        // The ban lives in its own table and reaches the identity as a restriction
        // with a window; the user's own column stays NotSpecified forever
        user.AccessRestrictions = [
            new AccessRestriction(AccessPolicy.DemocraticBan, now.AddDays(-1), now.AddDays(1))
        ];

        _cryptoService.Setup(c => c.Decrypt("token"))
            .ReturnsAsync($"{{\"userId\":\"{userId}\",\"sessionId\":\"{sessionId}\"}}");
        _repository.Setup(r => r.FindUser(userId)).ReturnsAsync(user);
        _repository.Setup(r => r.FindUserSettings(userId)).ReturnsAsync(UserSettings.Default);
        _repository.Setup(r => r.FindUserSession(userId, sessionId))
            .ReturnsAsync(new Session { Id = sessionId, ExpirationUtc = now.AddDays(1).UtcDateTime });

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
        _dateTimeProvider.Setup(d => d.Now).Returns(now);

        var user = Create.User(userId).WithRole(UserRole.RegularUser).Please();
        user.AccessRestrictions = [
            new AccessRestriction(AccessPolicy.DemocraticBan, now.AddDays(startsInDays), now.AddDays(endsInDays))
        ];

        _cryptoService.Setup(c => c.Decrypt("token"))
            .ReturnsAsync($"{{\"userId\":\"{userId}\",\"sessionId\":\"{sessionId}\"}}");
        _repository.Setup(r => r.FindUser(userId)).ReturnsAsync(user);
        _repository.Setup(r => r.FindUserSettings(userId)).ReturnsAsync(UserSettings.Default);
        _repository.Setup(r => r.FindUserSession(userId, sessionId))
            .ReturnsAsync(new Session { Id = sessionId, ExpirationUtc = now.AddDays(1).UtcDateTime });

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
        _dateTimeProvider.Setup(d => d.Now).Returns(now);

        var user = Create.User()
            .WithRole(UserRole.RegularUser)
            .WithCredentials("salt", "hash")
            .Please();
        user.AccessRestrictions = [
            new AccessRestriction(AccessPolicy.FullBan, now.AddHours(-1), now.AddHours(1))
        ];

        _repository.Setup(r => r.IsPendingRegistration(email)).ReturnsAsync(false);
        _loginAttemptTracker.Setup(t => t.IsAccountLocked(new LoginAttemptOrigin(email, null))).ReturnsAsync(false);
        _loginAttemptTracker.Setup(t => t.GetDelayForUser(new LoginAttemptOrigin(email, null))).ReturnsAsync(0);
        _repository.Setup(r => r.TryFindUserByEmail(email)).ReturnsAsync((true, user));
        var result = await _service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.Banned);
        // A ban that comes from a row decides as early as the flag does
        _securityManager.Verify(s => s.ComparePasswords(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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

        _identityProvider.Setup(p => p.Current).Returns(identity);

        var result = await _service.Logout();

        result.User.IsAuthenticated.Should().BeFalse();
        _repository.Verify(r => r.RemoveSession(userId, sessionId), Times.Once);
        _repository.Verify(r => r.UpdateActivity(userId, It.IsAny<DateTimeOffset>()), Times.Once);
        _auditService.Verify(a => a.LogAsync(userId, SecurityEventType.Logout, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
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

        _identityProvider.Setup(p => p.Current).Returns(identity);

        var result = await _service.LogoutElsewhere();

        result.Should().Be(identity);
        _repository.Verify(r => r.RemoveSessionsExcept(userId, sessionId), Times.Once);
        _auditService.Verify(a => a.LogAsync(userId, SecurityEventType.LogoutElsewhere, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
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

        _identityProvider.Setup(p => p.Current).Returns(identity);

        await _service.TerminateSession(userId, targetSessionId);

        _repository.Verify(r => r.RemoveSession(userId, targetSessionId), Times.Once);
        _auditService.Verify(a => a.LogAsync(userId, SecurityEventType.SessionTerminated,
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
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

        _identityProvider.Setup(p => p.Current).Returns(identity);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.TerminateSession(otherUserId, sessionId));
    }
}
