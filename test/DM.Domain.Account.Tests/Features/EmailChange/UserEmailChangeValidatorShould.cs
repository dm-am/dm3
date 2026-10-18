using System.Threading.Tasks;
using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Core.Identity;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.EmailChange;

public class UserEmailChangeValidatorShould : UnitTestBase
{
    private readonly UserEmailChangeValidator validator;
    private readonly IEmailChangeRepository repository;
    private readonly ISecurityManager securityManager;

    public UserEmailChangeValidatorShould()
    {
        repository = Mock<IEmailChangeRepository>();
        securityManager = Mock<ISecurityManager>();

        var authenticatedUser = new AuthenticatedUser
        {
            UserId = System.Guid.NewGuid(),
            Username = "testuser",
            Email = "old@example.com",
            Salt = "salt",
            PasswordHash = "hash"
        };

        repository.FindUser("testuser").Returns(authenticatedUser);

        repository.IsEmailFree(Arg.Any<string>(), default).Returns(true);

        securityManager.ComparePasswords("correctpassword", "salt", "hash").Returns(true);
        securityManager.ComparePasswords(Arg.Is<string>(p => p != "correctpassword"), "salt", "hash").Returns(false);

        validator = new UserEmailChangeValidator(repository, securityManager);
    }

    [Fact]
    public async Task PassForValidInput()
    {
        var input = new UserEmailChange
        {
            Username = "testuser",
            Password = "correctpassword",
            Email = "new@example.com"
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task FailWhenUsernameIsEmpty()
    {
        var input = new UserEmailChange { Username = "" };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenPasswordIsEmpty()
    {
        var input = new UserEmailChange
        {
            Username = "testuser",
            Password = "",
            Email = "new@example.com"
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenEmailIsEmpty()
    {
        var input = new UserEmailChange
        {
            Username = "testuser",
            Password = "correctpassword",
            Email = ""
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenEmailIsInvalid()
    {
        var input = new UserEmailChange
        {
            Username = "testuser",
            Password = "correctpassword",
            Email = "not-an-email"
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public async Task FailWhenUserNotFound()
    {
        var input = new UserEmailChange
        {
            Username = "nonexistent",
            Password = "password",
            Email = "new@example.com"
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public async Task FailWhenPasswordIsIncorrect()
    {
        var input = new UserEmailChange
        {
            Username = "testuser",
            Password = "wrongpassword",
            Email = "new@example.com"
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage(ValidationError.Invalid);
    }

    /// <summary>
    /// A wrong password learns nothing about the new address: "taken" is
    /// said only after the password matched.
    /// </summary>
    [Fact]
    public async Task NotSayTakenToAWrongPassword()
    {
        repository.IsEmailFree("held@example.com", default).Returns(false);
        var input = new UserEmailChange
        {
            Username = "testuser",
            Password = "wrongpassword",
            Email = "held@example.com"
        };

        var result = await validator.TestValidateAsync(input);

        result.ShouldHaveValidationErrorFor(u => u.Password);
        result.ShouldNotHaveValidationErrorFor(u => u.Email);
    }

    [Fact]
    public async Task SayTakenOnlyAfterTheMatchedPassword()
    {
        repository.IsEmailFree("held@example.com", default).Returns(false);
        var input = new UserEmailChange
        {
            Username = "testuser",
            Password = "correctpassword",
            Email = "held@example.com"
        };

        var result = await validator.TestValidateAsync(input);

        result.ShouldHaveValidationErrorFor(u => u.Email).WithErrorMessage(ValidationError.Taken);
    }
}
