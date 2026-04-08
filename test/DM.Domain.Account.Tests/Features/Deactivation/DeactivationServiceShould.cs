using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Deactivation;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Deactivation;

public class DeactivationServiceShould : UnitTestBase
{
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<ISecurityManager> _securityManager;
    private readonly Mock<IAuthenticationService> _authenticationService;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly Mock<IDeactivationRepository> _repository;
    private readonly DeactivationService _service;

    public DeactivationServiceShould()
    {
        _identityProvider = Mock<IIdentityProvider>();
        _securityManager = Mock<ISecurityManager>();
        _authenticationService = Mock<IAuthenticationService>();
        _eventProducer = Mock<IEventProducer>();
        _repository = Mock<IDeactivationRepository>();

        _service = new DeactivationService(
            _identityProvider.Object,
            _securityManager.Object,
            _authenticationService.Object,
            _eventProducer.Object,
            _repository.Object);
    }

    [Fact]
    public async Task ThrowWhenUserIsNotAuthenticated()
    {
        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.Deactivate("password"));

        exception.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ThrowWhenUserNotFound()
    {
        var userId = Guid.NewGuid();
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = userId, Role = UserRole.RegularUser },
            new Session { Id = Guid.NewGuid() },
            UserSettings.Default,
            "token");

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _repository.Setup(r => r.GetUserCredentials(userId, It.IsAny<CancellationToken>())).ReturnsAsync((UserCredentials?)null);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.Deactivate("password"));

        exception.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ThrowWhenPasswordIsIncorrect()
    {
        var userId = Guid.NewGuid();
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = userId, Role = UserRole.RegularUser },
            new Session { Id = Guid.NewGuid() },
            UserSettings.Default,
            "token");
        var credentials = new UserCredentials
        {
            UserId = userId,
            Salt = "salt",
            PasswordHash = "hash"
        };

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _repository.Setup(r => r.GetUserCredentials(userId, It.IsAny<CancellationToken>())).ReturnsAsync(credentials);
        _securityManager.Setup(s => s.ComparePasswords("wrongpassword", credentials.Salt, credentials.PasswordHash))
            .Returns(false);

        var exception = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => _service.Deactivate("wrongpassword"));

        exception.ValidationErrors.Should().ContainKey("password");
    }

    [Fact]
    public async Task DeactivateUserAndTerminateAllSessions()
    {
        var userId = Guid.NewGuid();
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = userId, Role = UserRole.RegularUser },
            new Session { Id = Guid.NewGuid() },
            UserSettings.Default,
            "token");
        var credentials = new UserCredentials
        {
            UserId = userId,
            Salt = "salt",
            PasswordHash = "hash"
        };

        _identityProvider.Setup(p => p.Current).Returns(identity);
        _repository.Setup(r => r.GetUserCredentials(userId, It.IsAny<CancellationToken>())).ReturnsAsync(credentials);
        _securityManager.Setup(s => s.ComparePasswords("correctpassword", credentials.Salt, credentials.PasswordHash))
            .Returns(true);

        await _service.Deactivate("correctpassword");

        _repository.Verify(r => r.DeactivateUser(userId, It.IsAny<CancellationToken>()), Times.Once);
        _authenticationService.Verify(a => a.LogoutAll(userId), Times.Once);
        _eventProducer.Verify(e => e.SendAsync(EventType.AccountDeactivated, userId), Times.Once);
    }
}
