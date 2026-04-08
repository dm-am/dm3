using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.PasswordChange;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DM.Domain.Account.Tests.Features.PasswordChange;

public class PasswordChangeServiceShould : UnitTestBase
{
    private readonly Mock<IValidator<UserPasswordChange>> _validator;
    private readonly Mock<IPasswordChangeRepository> _repository;
    private readonly Mock<IAuthenticationService> _authenticationService;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<ISecurityManager> _securityManager;
    private readonly Mock<ICompromisedPasswordChecker> _compromisedPasswordChecker;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly Mock<IPasswordChangeMailSender> _notificationSender;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly PasswordChangeService _service;

    public PasswordChangeServiceShould()
    {
        _validator = Mock<IValidator<UserPasswordChange>>();
        _repository = Mock<IPasswordChangeRepository>();
        _authenticationService = Mock<IAuthenticationService>();
        _identityProvider = Mock<IIdentityProvider>();
        _securityManager = Mock<ISecurityManager>();
        _compromisedPasswordChecker = Mock<ICompromisedPasswordChecker>();
        _eventProducer = Mock<IEventProducer>();
        _notificationSender = Mock<IPasswordChangeMailSender>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        var config = Options.Create(new TokenConfiguration
        {
            PasswordResetTokenLifetimeHours = 24
        });

        _validator.Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<UserPasswordChange>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _service = new PasswordChangeService(
            _validator.Object,
            _securityManager.Object,
            _compromisedPasswordChecker.Object,
            _repository.Object,
            _authenticationService.Object,
            _identityProvider.Object,
            _eventProducer.Object,
            _notificationSender.Object,
            _dateTimeProvider.Object,
            config);
    }

    [Fact]
    public async Task ThrowWhenNotAuthenticatedAndNoToken()
    {
        var passwordChange = new UserPasswordChange
        {
            OldPassword = "oldpass",
            NewPassword = "newpass"
        };

        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.Change(passwordChange));

        exception.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ThrowWhenNewPasswordMatchesCurrentPassword()
    {
        var userId = Guid.NewGuid();
        var passwordChange = new UserPasswordChange
        {
            OldPassword = "samepass",
            NewPassword = "samepass"
        };

        _identityProvider.Setup(p => p.Current)
            .Returns(Identity.Success(
                new AuthenticatedUser { UserId = userId, Role = UserRole.RegularUser, Salt = "salt", PasswordHash = "hash" },
                new Session { Id = Guid.NewGuid() },
                UserSettings.Default,
                "token"));
        _securityManager.Setup(s => s.ComparePasswords("samepass", "salt", "hash"))
            .Returns(true);

        var exception = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => _service.Change(passwordChange));

        exception.ValidationErrors.Should().ContainKey(nameof(passwordChange.NewPassword));
    }

    [Fact]
    public async Task ThrowWhenPasswordIsCompromised()
    {
        var userId = Guid.NewGuid();
        var passwordChange = new UserPasswordChange
        {
            OldPassword = "oldpass",
            NewPassword = "compromised123"
        };

        _identityProvider.Setup(p => p.Current)
            .Returns(Identity.Success(
                new AuthenticatedUser { UserId = userId, Role = UserRole.RegularUser, Salt = "salt", PasswordHash = "hash" },
                new Session { Id = Guid.NewGuid() },
                UserSettings.Default,
                "token"));
        _securityManager.Setup(s => s.ComparePasswords(passwordChange.NewPassword, "salt", "hash"))
            .Returns(false);
        _compromisedPasswordChecker.Setup(c => c.IsCompromisedAsync(passwordChange.NewPassword))
            .ReturnsAsync(true);

        var exception = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => _service.Change(passwordChange));

        exception.ValidationErrors.Should().ContainKey(nameof(passwordChange.NewPassword));
    }

    [Fact]
    public async Task ChangePasswordAndLogoutElsewhereForAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        var passwordChange = new UserPasswordChange
        {
            OldPassword = "oldpass",
            NewPassword = "newpass123"
        };

        _identityProvider.Setup(p => p.Current)
            .Returns(Identity.Success(
                new AuthenticatedUser { UserId = userId, Username = "testuser", Email = "test@example.com", Role = UserRole.RegularUser, Salt = "salt", PasswordHash = "hash" },
                new Session { Id = Guid.NewGuid() },
                UserSettings.Default,
                "token"));
        _securityManager.Setup(s => s.ComparePasswords(passwordChange.NewPassword, "salt", "hash"))
            .Returns(false);
        _compromisedPasswordChecker.Setup(c => c.IsCompromisedAsync(passwordChange.NewPassword))
            .ReturnsAsync(false);
        _securityManager.Setup(s => s.GeneratePassword(passwordChange.NewPassword))
            .Returns(("newhash", "newsalt"));

        var result = await _service.Change(passwordChange);

        result.UserId.Should().Be(userId);
        _repository.Verify(r => r.UpdatePassword(userId, "newhash", "newsalt", null), Times.Once);
        _authenticationService.Verify(a => a.LogoutElsewhere(), Times.Once);
        _authenticationService.Verify(a => a.LogoutAll(It.IsAny<Guid>()), Times.Never);
        _eventProducer.Verify(e => e.SendAsync(EventType.PasswordChanged, userId), Times.Once);
        _notificationSender.Verify(n => n.Send("test@example.com", "testuser"), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAndLogoutAllForTokenBasedReset()
    {
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var user = new AuthenticatedUser
        {
            UserId = userId,
            Username = "testuser",
            Email = "test@example.com",
            Role = UserRole.RegularUser,
            Salt = "salt",
            PasswordHash = "hash"
        };

        var passwordChange = new UserPasswordChange
        {
            Token = tokenId,
            NewPassword = "newpass123"
        };

        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());
        _repository.Setup(r => r.FindUser(tokenId)).ReturnsAsync(user);
        _securityManager.Setup(s => s.ComparePasswords(passwordChange.NewPassword, user.Salt, user.PasswordHash))
            .Returns(false);
        _compromisedPasswordChecker.Setup(c => c.IsCompromisedAsync(passwordChange.NewPassword))
            .ReturnsAsync(false);
        _securityManager.Setup(s => s.GeneratePassword(passwordChange.NewPassword))
            .Returns(("newhash", "newsalt"));

        var result = await _service.Change(passwordChange);

        result.UserId.Should().Be(userId);
        _repository.Verify(r => r.UpdatePassword(userId, "newhash", "newsalt", tokenId), Times.Once);
        _authenticationService.Verify(a => a.LogoutAll(userId), Times.Once);
        _authenticationService.Verify(a => a.LogoutElsewhere(), Times.Never);
        _eventProducer.Verify(e => e.SendAsync(EventType.PasswordChanged, userId), Times.Once);
    }

    [Fact]
    public async Task ReturnTokenInfoReadyForValidToken()
    {
        var tokenId = Guid.NewGuid();
        _repository.Setup(r => r.TokenValid(tokenId, It.IsAny<DateTimeOffset>())).ReturnsAsync(true);

        var result = await _service.GetTokenInfo(tokenId);

        result.Should().NotBeNull();
        result!.Status.Should().Be("ready");
    }

    [Fact]
    public async Task ReturnNullForNonExistentToken()
    {
        var tokenId = Guid.NewGuid();
        _repository.Setup(r => r.TokenValid(tokenId, It.IsAny<DateTimeOffset>())).ReturnsAsync(false);
        _repository.Setup(r => r.FindUser(tokenId)).ReturnsAsync((AuthenticatedUser?)null);

        var result = await _service.GetTokenInfo(tokenId);

        result.Should().BeNull();
    }
}
