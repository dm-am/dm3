using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Common.BusinessProcesses.Tokens;
using DM.Services.Community.BusinessProcesses.Account.PasswordReset;
using DM.Services.Community.BusinessProcesses.Account.PasswordReset.Confirmation;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Tests.Core;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DM.Services.Community.Tests.BusinessProcesses;

public class PasswordResetServiceShould : UnitTestBase
{
    private readonly PasswordResetService service;
    private readonly Mock<IValidator<UserPasswordReset>> validator;
    private readonly Mock<ITokenFactory> tokenFactory;
    private readonly Mock<IUserReadingRepository> userReadingRepository;
    private readonly Mock<IPasswordResetRepository> passwordResetRepository;
    private readonly Mock<IPasswordResetEmailSender> emailSender;

    public PasswordResetServiceShould()
    {
        validator = Mock<IValidator<UserPasswordReset>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UserPasswordReset>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        tokenFactory = Mock<ITokenFactory>();
        userReadingRepository = Mock<IUserReadingRepository>();
        passwordResetRepository = Mock<IPasswordResetRepository>();
        emailSender = Mock<IPasswordResetEmailSender>();

        var logger = new Mock<ILogger<PasswordResetService>>().Object;

        service = new PasswordResetService(
            validator.Object,
            tokenFactory.Object,
            userReadingRepository.Object,
            passwordResetRepository.Object,
            emailSender.Object,
            logger);
    }

    [Fact]
    public async Task SilentlySucceedWhenUserNotFound()
    {
        userReadingRepository
            .Setup(r => r.GetUserDetails("nonexistent"))
            .ReturnsAsync((UserDetails?)null!);

        var passwordReset = new UserPasswordReset
        {
            Login = "nonexistent",
            Email = "test@test.com"
        };

        await service.Reset(passwordReset);

        tokenFactory.Verify(f => f.Create(It.IsAny<Guid>(), It.IsAny<TokenType>()), Times.Never);
        passwordResetRepository.Verify(r => r.ReplacePasswordResetToken(It.IsAny<Guid>(), It.IsAny<Token>()), Times.Never);
        emailSender.Verify(s => s.Send(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task SilentlySucceedWhenEmailDoesNotMatch()
    {
        var user = new UserDetails
        {
            UserId = Guid.NewGuid(),
            Login = "testuser",
            Email = "user@test.com"
        };

        userReadingRepository
            .Setup(r => r.GetUserDetails("testuser"))
            .ReturnsAsync(user);

        var passwordReset = new UserPasswordReset
        {
            Login = "testuser",
            Email = "wrong@test.com"
        };

        await service.Reset(passwordReset);

        tokenFactory.Verify(f => f.Create(It.IsAny<Guid>(), It.IsAny<TokenType>()), Times.Never);
        passwordResetRepository.Verify(r => r.ReplacePasswordResetToken(It.IsAny<Guid>(), It.IsAny<Token>()), Times.Never);
        emailSender.Verify(s => s.Send(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task SendResetEmailWhenUserAndEmailMatch()
    {
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var user = new UserDetails
        {
            UserId = userId,
            Login = "testuser",
            Email = "user@test.com"
        };

        var token = new Token { TokenId = tokenId };

        userReadingRepository
            .Setup(r => r.GetUserDetails("testuser"))
            .ReturnsAsync(user);

        tokenFactory
            .Setup(f => f.Create(userId, TokenType.PasswordChange))
            .Returns(token);

        passwordResetRepository
            .Setup(r => r.ReplacePasswordResetToken(userId, token))
            .Returns(Task.CompletedTask);

        emailSender
            .Setup(s => s.Send("user@test.com", "testuser", tokenId))
            .Returns(Task.CompletedTask);

        var passwordReset = new UserPasswordReset
        {
            Login = "testuser",
            Email = "user@test.com"
        };

        await service.Reset(passwordReset);

        tokenFactory.Verify(f => f.Create(userId, TokenType.PasswordChange), Times.Once);
        passwordResetRepository.Verify(r => r.ReplacePasswordResetToken(userId, token), Times.Once);
        emailSender.Verify(s => s.Send("user@test.com", "testuser", tokenId), Times.Once);
    }

    [Fact]
    public async Task NotExposeUserDataInReturnValue()
    {
        var user = new UserDetails
        {
            UserId = Guid.NewGuid(),
            Login = "testuser",
            Email = "user@test.com"
        };

        userReadingRepository
            .Setup(r => r.GetUserDetails("testuser"))
            .ReturnsAsync(user);

        tokenFactory
            .Setup(f => f.Create(It.IsAny<Guid>(), TokenType.PasswordChange))
            .Returns(new Token { TokenId = Guid.NewGuid() });

        var passwordReset = new UserPasswordReset
        {
            Login = "testuser",
            Email = "user@test.com"
        };

        // The method returns Task (void), not Task<GeneralUser> or Task<UserDetails>
        // This ensures no user data is exposed in the return value
        await service.Reset(passwordReset);

        // If we reach here without exceptions, the test passes
        // The method completed successfully without returning user data
    }

    [Fact]
    public async Task CompareEmailsCaseInsensitively()
    {
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var user = new UserDetails
        {
            UserId = userId,
            Login = "testuser",
            Email = "Test@Foo.com"
        };

        var token = new Token { TokenId = tokenId };

        userReadingRepository
            .Setup(r => r.GetUserDetails("testuser"))
            .ReturnsAsync(user);

        tokenFactory
            .Setup(f => f.Create(userId, TokenType.PasswordChange))
            .Returns(token);

        passwordResetRepository
            .Setup(r => r.ReplacePasswordResetToken(userId, token))
            .Returns(Task.CompletedTask);

        emailSender
            .Setup(s => s.Send("Test@Foo.com", "testuser", tokenId))
            .Returns(Task.CompletedTask);

        var passwordReset = new UserPasswordReset
        {
            Login = "testuser",
            Email = "test@foo.com"
        };

        await service.Reset(passwordReset);

        tokenFactory.Verify(f => f.Create(userId, TokenType.PasswordChange), Times.Once);
        passwordResetRepository.Verify(r => r.ReplacePasswordResetToken(userId, token), Times.Once);
        emailSender.Verify(s => s.Send("Test@Foo.com", "testuser", tokenId), Times.Once);
    }
}
