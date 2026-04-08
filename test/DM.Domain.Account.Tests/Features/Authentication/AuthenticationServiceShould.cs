using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
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
        _loginAttemptTracker.Setup(t => t.IsAccountLocked(email)).ReturnsAsync(true);
        _loginAttemptTracker.Setup(t => t.GetRemainingLockoutSeconds(email)).ReturnsAsync(300);

        var result = await _service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.AccountLocked);
    }

    [Fact]
    public async Task ReturnFailureWhenUserNotFound()
    {
        var email = "test@example.com";
        _repository.Setup(r => r.IsPendingRegistration(email)).ReturnsAsync(false);
        _loginAttemptTracker.Setup(t => t.IsAccountLocked(email)).ReturnsAsync(false);
        _loginAttemptTracker.Setup(t => t.GetDelayForUser(email)).ReturnsAsync(0);
        _repository.Setup(r => r.TryFindUserByEmail(email)).ReturnsAsync(((bool, AuthenticatedUser?))(false, null));

        var result = await _service.Authenticate(email, "password");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.WrongLogin);
        _loginAttemptTracker.Verify(t => t.RecordFailedAttempt(email), Times.Once);
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
        _loginAttemptTracker.Setup(t => t.IsAccountLocked(email)).ReturnsAsync(false);
        _loginAttemptTracker.Setup(t => t.GetDelayForUser(email)).ReturnsAsync(0);
        _repository.Setup(r => r.TryFindUserByEmail(email)).ReturnsAsync((true, user));
        _securityManager.Setup(s => s.ComparePasswords("wrongpassword", user.Salt, user.PasswordHash))
            .Returns(false);

        var result = await _service.Authenticate(email, "wrongpassword");

        result.User.IsAuthenticated.Should().BeFalse();
        result.Error.Should().Be(AuthenticationError.WrongPassword);
        _loginAttemptTracker.Verify(t => t.RecordFailedAttempt(email), Times.Once);
        _auditService.Verify(a => a.LogAsync(user.UserId, SecurityEventType.LoginFailure,
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
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
        _loginAttemptTracker.Setup(t => t.IsAccountLocked(email)).ReturnsAsync(false);
        _loginAttemptTracker.Setup(t => t.GetDelayForUser(email)).ReturnsAsync(0);
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
