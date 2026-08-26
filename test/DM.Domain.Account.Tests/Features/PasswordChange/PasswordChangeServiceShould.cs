using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.PasswordChange;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.TwoFactor;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Account.Tests.Features.PasswordChange;

public class PasswordChangeServiceShould : UnitTestBase
{
    private readonly IValidator<UserPasswordChange> _validator;
    private readonly IPasswordChangeRepository _repository;
    private readonly IAuthenticationService _authenticationService;
    private readonly IIdentityProvider _identityProvider;
    private readonly ISecurityManager _securityManager;
    private readonly ICompromisedPasswordChecker _compromisedPasswordChecker;
    private readonly IEventProducer _eventProducer;
    private readonly IPasswordChangeMailSender _notificationSender;
    private readonly ISecurityAuditRepository _auditService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITwoFactorRepository _twoFactorRepository;
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
        _auditService = Mock<ISecurityAuditRepository>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _twoFactorRepository = Mock<ITwoFactorRepository>();
        var config = Options.Create(new TokenConfiguration
        {
            PasswordResetTokenLifetimeHours = 24
        });

        _validator.ValidateAsync(
                Arg.Any<ValidationContext<UserPasswordChange>>(),
                Arg.Any<CancellationToken>()).Returns(new ValidationResult());

        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _service = new PasswordChangeService(
            _validator,
            _securityManager,
            _compromisedPasswordChecker,
            _repository,
            _authenticationService,
            _identityProvider,
            _eventProducer,
            _notificationSender,
            _auditService,
            _dateTimeProvider,
            _twoFactorRepository,
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

        _identityProvider.Current.Returns(Identity.Guest());

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

        _identityProvider.Current.Returns(Identity.Success(
                new AuthenticatedUser { UserId = userId, Role = UserRole.RegularUser, Salt = "salt", PasswordHash = "hash" },
                new Session { Id = Guid.NewGuid() },
                UserSettings.Default,
                "token"));
        _securityManager.ComparePasswords("samepass", "salt", "hash").Returns(true);

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

        _identityProvider.Current.Returns(Identity.Success(
                new AuthenticatedUser { UserId = userId, Role = UserRole.RegularUser, Salt = "salt", PasswordHash = "hash" },
                new Session { Id = Guid.NewGuid() },
                UserSettings.Default,
                "token"));
        _securityManager.ComparePasswords(passwordChange.NewPassword, "salt", "hash").Returns(false);
        _compromisedPasswordChecker.IsCompromisedAsync(passwordChange.NewPassword).Returns(true);

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

        _identityProvider.Current.Returns(Identity.Success(
                new AuthenticatedUser { UserId = userId, Username = "testuser", Email = "test@example.com", Role = UserRole.RegularUser, Salt = "salt", PasswordHash = "hash" },
                new Session { Id = Guid.NewGuid() },
                UserSettings.Default,
                "token"));
        _securityManager.ComparePasswords(passwordChange.NewPassword, "salt", "hash").Returns(false);
        _compromisedPasswordChecker.IsCompromisedAsync(passwordChange.NewPassword).Returns(false);
        _securityManager.GeneratePassword(passwordChange.NewPassword).Returns(("newhash", "newsalt"));

        var result = await _service.Change(passwordChange);

        result.UserId.Should().Be(userId);
        await _repository.Received(1).UpdatePassword(userId, "newhash", "newsalt", null);
        await _authenticationService.Received(1).LogoutElsewhere();
        await _authenticationService.DidNotReceive().LogoutAll(Arg.Any<Guid>());
        await _eventProducer.Received(1).SendAsync(EventType.PasswordChanged, userId);
        await _notificationSender.Received(1).Send("test@example.com", "testuser");
        // AC-18 and INV-8: a login begun with the old password does not outlive
        // it. Changing a password is a statement that the account may be
        // compromised, and a half-finished sign-in that survives the statement is
        // one the statement did not cover.
        await _twoFactorRepository.Received(1).RemoveChallengesOf(
            userId, Arg.Any<CancellationToken>());
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

        _identityProvider.Current.Returns(Identity.Guest());
        _repository.FindUser(tokenId, Arg.Any<DateTimeOffset>()).Returns(user);
        _securityManager.ComparePasswords(passwordChange.NewPassword, user.Salt, user.PasswordHash).Returns(false);
        _compromisedPasswordChecker.IsCompromisedAsync(passwordChange.NewPassword).Returns(false);
        _securityManager.GeneratePassword(passwordChange.NewPassword).Returns(("newhash", "newsalt"));

        var result = await _service.Change(passwordChange);

        result.UserId.Should().Be(userId);
        await _repository.Received(1).UpdatePassword(userId, "newhash", "newsalt", tokenId);
        await _authenticationService.Received(1).LogoutAll(userId);
        await _authenticationService.DidNotReceive().LogoutElsewhere();
        await _eventProducer.Received(1).SendAsync(EventType.PasswordChanged, userId);
        // AC-18 by the other road: a reset from the mailbox kills the unfinished
        // logins of the account just as the form inside a session does.
        await _twoFactorRepository.Received(1).RemoveChallengesOf(
            userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnTokenInfoReadyForValidToken()
    {
        var tokenId = Guid.NewGuid();
        _repository.TokenValid(tokenId, Arg.Any<DateTimeOffset>()).Returns(true);

        var result = await _service.GetTokenInfo(tokenId);

        result.Should().NotBeNull();
        result!.Status.Should().Be("ready");
    }

    [Fact]
    public async Task ReturnNullForNonExistentToken()
    {
        var tokenId = Guid.NewGuid();
        _repository.TokenValid(tokenId, Arg.Any<DateTimeOffset>()).Returns(false);
        _repository.FindUser(tokenId, Arg.Any<DateTimeOffset>()).Returns((AuthenticatedUser?)null);

        var result = await _service.GetTokenInfo(tokenId);

        result.Should().BeNull();
    }
}
