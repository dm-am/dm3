using System;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.PasswordChange;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Testing;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace DM.Domain.Account.Tests.Features.PasswordChange;

public class UserPasswordChangeValidatorShould : UnitTestBase
{
    private readonly UserPasswordChangeValidator validator;
    private readonly Mock<IPasswordChangeRepository> repository;
    private readonly Mock<IDateTimeProvider> dateTimeProvider;
    private readonly Mock<IIdentityProvider> identityProvider;
    private readonly Mock<ISecurityManager> securityManager;

    public UserPasswordChangeValidatorShould()
    {
        repository = Mock<IPasswordChangeRepository>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        identityProvider = Mock<IIdentityProvider>();
        securityManager = Mock<ISecurityManager>();

        var now = DateTimeOffset.UtcNow;
        dateTimeProvider.Setup(d => d.Now).Returns(now);

        var authenticatedUser = new AuthenticatedUser
        {
            UserId = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            Role = UserRole.RegularUser,
            Salt = "salt",
            PasswordHash = "hash"
        };

        identityProvider.Setup(p => p.Current).Returns(Identity.Success(authenticatedUser, new Session { Id = Guid.NewGuid() }, UserSettings.Default, "token"));

        securityManager.Setup(s => s.ComparePasswords("correctpassword", "salt", "hash"))
            .Returns(true);
        securityManager.Setup(s => s.ComparePasswords(It.Is<string>(p => p != "correctpassword"), "salt", "hash"))
            .Returns(false);

        repository.Setup(r => r.TokenValid(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(true);

        var passwordPolicy = Options.Create(new PasswordPolicyConfiguration
        {
            MinimumLength = 8,
            MaximumLength = 128,
            RequireUppercase = false,
            RequireLowercase = false,
            RequireDigit = false,
            RequireSpecialCharacter = false
        });

        var tokenConfig = Options.Create(new TokenConfiguration
        {
            PasswordResetTokenLifetimeHours = 24
        });

        validator = new UserPasswordChangeValidator(
            repository.Object,
            dateTimeProvider.Object,
            identityProvider.Object,
            securityManager.Object,
            passwordPolicy,
            tokenConfig);
    }

    [Fact]
    public async Task PassForValidInputWithToken()
    {
        var input = new UserPasswordChange
        {
            Token = Guid.NewGuid(),
            NewPassword = "newpassword123"
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task PassForValidInputWithOldPassword()
    {
        var input = new UserPasswordChange
        {
            OldPassword = "correctpassword",
            NewPassword = "newpassword123"
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenNewPasswordIsEmpty()
    {
        var input = new UserPasswordChange
        {
            OldPassword = "correctpassword",
            NewPassword = ""
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenNewPasswordIsTooShort()
    {
        var input = new UserPasswordChange
        {
            OldPassword = "correctpassword",
            NewPassword = "short"
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage(ValidationError.Short);
    }

    [Fact]
    public void FailWhenNewPasswordIsTooLong()
    {
        var input = new UserPasswordChange
        {
            OldPassword = "correctpassword",
            NewPassword = new string('a', 129)
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenOldPasswordIsEmpty()
    {
        var input = new UserPasswordChange
        {
            OldPassword = "",
            NewPassword = "newpassword123"
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.OldPassword)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenOldPasswordIsIncorrect()
    {
        var input = new UserPasswordChange
        {
            OldPassword = "wrongpassword",
            NewPassword = "newpassword123"
        };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.OldPassword)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public async Task FailWhenTokenIsInvalid()
    {
        repository.Setup(r => r.TokenValid(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(false);

        var input = new UserPasswordChange
        {
            Token = Guid.NewGuid(),
            NewPassword = "newpassword123"
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor("Token.Value")
            .WithErrorMessage(ValidationError.Invalid);
    }
}
