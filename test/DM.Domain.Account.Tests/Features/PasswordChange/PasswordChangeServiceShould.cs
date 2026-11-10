using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.PasswordChange;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.TwoFactor;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.PasswordChange;

public class PasswordChangeServiceShould : UnitTestBase
{
    private readonly IValidator<UserPasswordChange> validator;
    private readonly IPasswordChangeRepository repository;
    private readonly ISessionService sessionService;
    private readonly IIdentityProvider identityProvider;
    private readonly ISecurityManager securityManager;
    private readonly ICompromisedPasswordChecker compromisedPasswordChecker;
    private readonly IEventProducer eventProducer;
    private readonly IPasswordChangeMailSender notificationSender;
    private readonly ISecurityAuditRepository auditService;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ITwoFactorRepository twoFactorRepository;
    private readonly PasswordChangeService service;

    public PasswordChangeServiceShould()
    {
        validator = Mock<IValidator<UserPasswordChange>>();
        repository = Mock<IPasswordChangeRepository>();
        sessionService = Mock<ISessionService>();
        identityProvider = Mock<IIdentityProvider>();
        securityManager = Mock<ISecurityManager>();
        compromisedPasswordChecker = Mock<ICompromisedPasswordChecker>();
        eventProducer = Mock<IEventProducer>();
        notificationSender = Mock<IPasswordChangeMailSender>();
        auditService = Mock<ISecurityAuditRepository>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        twoFactorRepository = Mock<ITwoFactorRepository>();
        var config = Options.Create(new TokenConfiguration
        {
            PasswordResetTokenLifetimeHours = 24
        });

        validator.ValidateAsync(
                Arg.Any<ValidationContext<UserPasswordChange>>(),
                Arg.Any<CancellationToken>()).Returns(new ValidationResult());

        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);
        repository.UpdatePassword(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>())
            .Returns(true);

        service = new PasswordChangeService(
            validator,
            securityManager,
            compromisedPasswordChecker,
            repository,
            sessionService,
            identityProvider,
            eventProducer,
            notificationSender,
            auditService,
            dateTimeProvider,
            twoFactorRepository,
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

        identityProvider.Current.Returns(Identity.Guest());

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.Change(passwordChange));

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

        SignIn(userId);
        securityManager.ComparePasswords("samepass", "salt", "hash").Returns(true);

        var exception = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => service.Change(passwordChange));

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

        SignIn(userId);
        securityManager.ComparePasswords(passwordChange.NewPassword, "salt", "hash").Returns(false);
        compromisedPasswordChecker.IsCompromisedAsync(passwordChange.NewPassword).Returns(true);

        var exception = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => service.Change(passwordChange));

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

        SignIn(userId);
        securityManager.ComparePasswords(passwordChange.NewPassword, "salt", "hash").Returns(false);
        compromisedPasswordChecker.IsCompromisedAsync(passwordChange.NewPassword).Returns(false);
        securityManager.GeneratePassword(passwordChange.NewPassword).Returns(("newhash", "newsalt"));

        var result = await service.Change(passwordChange);

        result.UserId.Should().Be(userId);
        await repository.Received(1).UpdatePassword(userId, "newhash", "newsalt", null);
        await sessionService.Received(1).LogoutElsewhere();
        await sessionService.DidNotReceive().LogoutAll(Arg.Any<Guid>());
        await eventProducer.Received(1).SendAsync(EventType.PasswordChanged, userId);
        await notificationSender.Received(1).Send("test@example.com");
        // A login begun with the old password does not outlive it. Changing a
        // password is a statement that the account may be compromised, and a
        // half-finished sign-in that survives the statement is one the
        // statement did not cover.
        await twoFactorRepository.Received(1).RemoveChallengesOf(
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

        identityProvider.Current.Returns(Identity.Guest());
        repository.FindUser(tokenId, Arg.Any<DateTimeOffset>()).Returns(user);
        securityManager.ComparePasswords(passwordChange.NewPassword, user.Salt, user.PasswordHash).Returns(false);
        compromisedPasswordChecker.IsCompromisedAsync(passwordChange.NewPassword).Returns(false);
        securityManager.GeneratePassword(passwordChange.NewPassword).Returns(("newhash", "newsalt"));

        var result = await service.Change(passwordChange);

        result.UserId.Should().Be(userId);
        await repository.Received(1).UpdatePassword(userId, "newhash", "newsalt", tokenId);
        await sessionService.Received(1).LogoutAll(userId);
        await sessionService.DidNotReceive().LogoutElsewhere();
        await eventProducer.Received(1).SendAsync(EventType.PasswordChanged, userId);
        // A reset from the mailbox kills the unfinished logins of the account
        // just as the form inside a session does.
        await twoFactorRepository.Received(1).RemoveChallengesOf(
            userId, Arg.Any<CancellationToken>());
    }

    // The account was removed between the lookup and the write: the password in the database is the old one.
    [Fact]
    public async Task RefuseWithoutEndingSessionsOrWritingWhenTheAccountIsGoneAtTheWrite()
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
        var passwordChange = new UserPasswordChange { Token = tokenId, NewPassword = "newpass123" };

        identityProvider.Current.Returns(Identity.Guest());
        repository.FindUser(tokenId, Arg.Any<DateTimeOffset>()).Returns(user);
        securityManager.GeneratePassword(passwordChange.NewPassword).Returns(("newhash", "newsalt"));
        repository.UpdatePassword(userId, "newhash", "newsalt", tokenId).Returns(false);

        var exception = await Assert.ThrowsAsync<HttpException>(() => service.Change(passwordChange));

        exception.StatusCode.Should().Be(HttpStatusCode.NotFound);
        exception.Message.Should().Be(RefusalMessage.UserNotFound);
        await twoFactorRepository.DidNotReceive().RemoveChallengesOf(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await sessionService.DidNotReceive().LogoutAll(Arg.Any<Guid>());
        await sessionService.DidNotReceive().LogoutElsewhere();
        await auditService.DidNotReceive().LogAsync(
            Arg.Any<Guid>(), Arg.Any<SecurityEventType>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
        await eventProducer.DidNotReceive().SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>());
        await notificationSender.DidNotReceive().Send(Arg.Any<string>());
    }

    [Fact]
    public async Task ReturnTokenInfoReadyForValidToken()
    {
        var tokenId = Guid.NewGuid();
        repository.TokenValid(tokenId, Arg.Any<DateTimeOffset>()).Returns(true);

        var result = await service.GetTokenInfo(tokenId);

        result!.Status.Should().Be("ready");
    }

    [Fact]
    public async Task ReturnNullForNonExistentToken()
    {
        var tokenId = Guid.NewGuid();
        repository.TokenValid(tokenId, Arg.Any<DateTimeOffset>()).Returns(false);
        repository.FindUser(tokenId, Arg.Any<DateTimeOffset>()).Returns((AuthenticatedUser?)null);

        var result = await service.GetTokenInfo(tokenId);

        result.Should().BeNull();
    }

    private void SignIn(Guid userId)
    {
        var identity = Identities.User(userId, "testuser");
        identity.User.Email = "test@example.com";
        identity.User.Salt = "salt";
        identity.User.PasswordHash = "hash";
        identityProvider.Current.Returns(identity);
    }
}
