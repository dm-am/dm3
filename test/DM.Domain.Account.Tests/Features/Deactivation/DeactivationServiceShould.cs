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
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Deactivation;

public class DeactivationServiceShould : UnitTestBase
{
    private readonly IIdentityProvider _identityProvider;
    private readonly ISecurityManager _securityManager;
    private readonly IAuthenticationService _authenticationService;
    private readonly IEventProducer _eventProducer;
    private readonly IDeactivationRepository _repository;
    private readonly DeactivationService _service;

    public DeactivationServiceShould()
    {
        _identityProvider = Mock<IIdentityProvider>();
        _securityManager = Mock<ISecurityManager>();
        _authenticationService = Mock<IAuthenticationService>();
        _eventProducer = Mock<IEventProducer>();
        _repository = Mock<IDeactivationRepository>();

        _service = new DeactivationService(
            _identityProvider,
            _securityManager,
            _authenticationService,
            _eventProducer,
            _repository);
    }

    [Fact]
    public async Task ThrowWhenUserIsNotAuthenticated()
    {
        _identityProvider.Current.Returns(Identity.Guest());

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

        _identityProvider.Current.Returns(identity);
        _repository.GetUserCredentials(userId, Arg.Any<CancellationToken>()).Returns((UserCredentials?)null);

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

        _identityProvider.Current.Returns(identity);
        _repository.GetUserCredentials(userId, Arg.Any<CancellationToken>()).Returns(credentials);
        _securityManager.ComparePasswords("wrongpassword", credentials.Salt, credentials.PasswordHash).Returns(false);

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

        _identityProvider.Current.Returns(identity);
        _repository.GetUserCredentials(userId, Arg.Any<CancellationToken>()).Returns(credentials);
        _securityManager.ComparePasswords("correctpassword", credentials.Salt, credentials.PasswordHash).Returns(true);

        await _service.Deactivate("correctpassword");

        await _repository.Received(1).DeactivateUser(userId, Arg.Any<CancellationToken>());
        await _authenticationService.Received(1).LogoutAll(userId);
        await _eventProducer.Received(1).SendAsync(EventType.AccountDeactivated, userId);
    }
}
