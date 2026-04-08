using System.Threading.Tasks;
using DM.Domain.Account.Features.EmailChange;
using DM.Domain.Core.Identity;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace DM.Domain.Account.Tests.Features.EmailChange;

public class UserEmailChangeValidatorShould : UnitTestBase
{
    private readonly UserEmailChangeValidator validator;
    private readonly Mock<IEmailChangeRepository> repository;
    private readonly Mock<ISecurityManager> securityManager;

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

        repository.Setup(r => r.FindUser("testuser"))
            .ReturnsAsync(authenticatedUser);

        repository.Setup(r => r.IsEmailFree(It.IsAny<string>(), default))
            .ReturnsAsync(true);

        securityManager.Setup(s => s.ComparePasswords("correctpassword", "salt", "hash"))
            .Returns(true);
        securityManager.Setup(s => s.ComparePasswords(It.Is<string>(p => p != "correctpassword"), "salt", "hash"))
            .Returns(false);

        validator = new UserEmailChangeValidator(repository.Object, securityManager.Object);
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
}
