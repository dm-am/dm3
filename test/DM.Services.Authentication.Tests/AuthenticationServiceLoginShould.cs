using System;
using System.Threading.Tasks;
using DM.Services.Authentication.Configuration;
using DM.Services.Authentication.Dto;
using DM.Services.Authentication.Factories;
using DM.Services.Authentication.Implementation;
using DM.Services.Authentication.Implementation.Security;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Authentication.Repositories;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;
using DM.Tests.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Language.Flow;
using Xunit;
using Session = DM.Services.DataAccess.BusinessObjects.Users.Session;

namespace DM.Services.Authentication.Tests;

public class AuthenticationServiceLoginShould : UnitTestBase
{
    private readonly ISetup<IAuthenticationRepository, Task<(bool Success, AuthenticatedUser? User)>>
        userSearchSetup;

    private readonly AuthenticationService service;
    private readonly Mock<IAuthenticationRepository> authenticationRepository;
    private readonly Mock<ISecurityManager> securityManager;
    private readonly Mock<ISessionFactory> sessionFactory;
    private readonly Mock<ISymmetricCryptoService> cryptoService;
    private const string Email = "test@example.com";

    public AuthenticationServiceLoginShould()
    {
        securityManager = Mock<ISecurityManager>();
        cryptoService = Mock<ISymmetricCryptoService>();
        authenticationRepository = Mock<IAuthenticationRepository>();
        // Mock pending registration check - return false (not pending)
        authenticationRepository.Setup(r => r.IsPendingRegistration(It.IsAny<string>())).ReturnsAsync(false);
        userSearchSetup = authenticationRepository.Setup(r => r.TryFindUserByEmail(It.IsAny<string>()));
        sessionFactory = Mock<ISessionFactory>();
        var identityProvider = Mock<IIdentityProvider>();
        identityProvider.Setup(p => p.Current).Returns(Identity.Guest);
        var loginAttemptTracker = Mock<ILoginAttemptTracker>();
        loginAttemptTracker.Setup(t => t.GetDelayForUser(It.IsAny<string>())).ReturnsAsync(0);
        var logger = Mock<ILogger<AuthenticationService>>();
        var authConfig = Options.Create(new AuthenticationConfiguration());
        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);
        var updateBuilderFactory = Mock<IUpdateBuilderFactory>();
        var updateBuilder = Mock<IUpdateBuilder<User>>();
        updateBuilder.Setup(b => b.Field(It.IsAny<System.Linq.Expressions.Expression<Func<User, DateTimeOffset?>>>(), It.IsAny<DateTimeOffset>())).Returns(updateBuilder.Object);
        updateBuilderFactory.Setup(f => f.Create<User>(It.IsAny<Guid>())).Returns(updateBuilder.Object);
        authenticationRepository
            .Setup(r => r.UpdateActivity(It.IsAny<IUpdateBuilder<User>>()))
            .Returns(Task.CompletedTask);
        service = new AuthenticationService(securityManager.Object, cryptoService.Object,
            authenticationRepository.Object, sessionFactory.Object, dateTimeProvider.Object, identityProvider.Object, updateBuilderFactory.Object, loginAttemptTracker.Object, logger.Object, authConfig);
    }

    [Fact]
    public async Task FailIfNoUserFoundByLogin()
    {
        userSearchSetup.ReturnsAsync((false, null!));
        var actual = await service.Authenticate(Email, "qwerty");

        actual.Error.Should().Be(AuthenticationError.WrongLogin);
    }

    [Fact]
    public async Task FailIfRemovedUserFound()
    {
        var user = new AuthenticatedUser {IsRemoved = true};
        userSearchSetup.ReturnsAsync((true, user));
        var actual = await service.Authenticate(Email, "qwerty");

        actual.Error.Should().Be(AuthenticationError.Removed);
        actual.User.Should().Be(AuthenticatedUser.Guest);
        actual.Session.Should().BeNull();
        actual.Settings.Should().Be(UserSettings.Default);
        actual.AuthenticationToken.Should().BeNull();
    }

    [Fact]
    public async Task FailIfBannedUserFound()
    {
        var user = new AuthenticatedUser
        {
            IsRemoved = false,
            AccessPolicy = AccessPolicy.FullBan | AccessPolicy.RestrictContentEditing
        };
        userSearchSetup.ReturnsAsync((true, user));
        var actual = await service.Authenticate(Email, "qwerty");

        actual.Error.Should().Be(AuthenticationError.Banned);
        actual.User.Should().Be(AuthenticatedUser.Guest);
        actual.Session.Should().BeNull();
        actual.Settings.Should().Be(UserSettings.Default);
        actual.AuthenticationToken.Should().BeNull();
    }

    [Fact]
    public async Task FailIfPasswordIsIncorrect()
    {
        var user = new AuthenticatedUser
        {
            IsRemoved = false,
            AccessPolicy = AccessPolicy.GlobalChatBan | AccessPolicy.DemocraticBan,
            PasswordHash = "hash",
            Salt = "salt"
        };
        userSearchSetup.ReturnsAsync((true, user));
        securityManager
            .Setup(m => m.ComparePasswords(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns(false);

        var actual = await service.Authenticate(Email, "qwerty");

        actual.Error.Should().Be(AuthenticationError.WrongPassword);
        actual.User.Should().Be(AuthenticatedUser.Guest);
        actual.Session.Should().BeNull();
        actual.Settings.Should().Be(UserSettings.Default);
        actual.AuthenticationToken.Should().BeNull();
        securityManager.Verify(m => m.ComparePasswords("qwerty", "salt", "hash", It.IsAny<int>()));
    }

    [Fact]
    public async Task SucceedAndCreateNewUserSession()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = new Session {Id = sessionId};
        var userSettings = new UserSettings();
        var user = new AuthenticatedUser
        {
            UserId = userId,
            IsRemoved = false,
            AccessPolicy = AccessPolicy.NotSpecified,
            PasswordHash = "hash",
            Salt = "salt"
        };
        var externalSession = new DM.Services.Authentication.Dto.Session();
        userSearchSetup.ReturnsAsync((true, user));
        securityManager
            .Setup(m => m.ComparePasswords(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns(true);
        sessionFactory
            .Setup(f => f.Create(It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns(session);
        authenticationRepository
            .Setup(r => r.FindUserSettings(It.IsAny<Guid>()))
            .ReturnsAsync(userSettings);
        authenticationRepository
            .Setup(r => r.AddSession(It.IsAny<Guid>(), It.IsAny<Session>()))
            .ReturnsAsync(externalSession);
        cryptoService
            .Setup(s => s.Encrypt(It.IsAny<string>()))
            .ReturnsAsync("token");

        var actual = await service.Authenticate(Email, "qwerty");

        actual.Error.Should().Be(AuthenticationError.NoError);
        actual.User.Should().Be(user);
        actual.Session.Should().BeEquivalentTo(externalSession);
        actual.Settings.Should().Be(userSettings);
        actual.AuthenticationToken.Should().Be("token");
        securityManager.Verify(m => m.ComparePasswords("qwerty", "salt", "hash", It.IsAny<int>()));
        // Default is persistent (rememberMe=true)
        sessionFactory.Verify(f => f.Create(true, false));
        authenticationRepository.Verify(r => r.FindUserSettings(userId), Times.Once);
        authenticationRepository.Verify(r => r.AddSession(userId, session), Times.Once);
    }

    [Fact]
    public async Task LoginUnconditionally()
    {
        var userId = Guid.NewGuid();
        var user = new AuthenticatedUser{UserId = userId};
        var session = new Session();
        var userSettings = new UserSettings();
        var externalSession = new DM.Services.Authentication.Dto.Session();
        authenticationRepository
            .Setup(r => r.FindUser(It.IsAny<Guid>()))
            .ReturnsAsync(user);
        sessionFactory
            .Setup(f => f.Create(It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns(session);
        authenticationRepository
            .Setup(r => r.FindUserSettings(It.IsAny<Guid>()))
            .ReturnsAsync(userSettings);
        cryptoService
            .Setup(s => s.Encrypt(It.IsAny<string>()))
            .ReturnsAsync("token");
        authenticationRepository
            .Setup(r => r.AddSession(It.IsAny<Guid>(), It.IsAny<Session>()))
            .ReturnsAsync(externalSession);

        var actual = await service.Authenticate(userId);

        actual.Error.Should().Be(AuthenticationError.NoError);
        actual.AuthenticationToken.Should().Be("token");

        authenticationRepository.Verify(r => r.AddSession(userId, session), Times.Once);
    }
}