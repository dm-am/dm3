using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Registration;

public class UserRegistrationValidatorShould : UnitTestBase
{
    private readonly UserRegistrationValidator validator;
    private readonly IRegistrationRepository repository;

    public UserRegistrationValidatorShould()
    {
        repository = Mock<IRegistrationRepository>();
        repository.EmailFreeForNewRegistration(Arg.Any<string>(), default).Returns(true);

        var passwordPolicy = Options.Create(new PasswordPolicyConfiguration
        {
            MinimumLength = 8,
            MaximumLength = 128,
            RequireUppercase = false,
            RequireLowercase = false,
            RequireDigit = false,
            RequireSpecialCharacter = false
        });

        validator = new UserRegistrationValidator(repository, passwordPolicy);
    }

    [Fact]
    public async Task PassForValidInput()
    {
        var input = new UserRegistration
        {
            Email = "user@example.com",
            Password = "password123",
            AcceptedRules = true
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task FailWhenEmailIsEmpty()
    {
        var input = new UserRegistration { Email = "" };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenEmailIsInvalid()
    {
        var input = new UserRegistration { Email = "not-an-email" };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public async Task FailWhenEmailExceedsMaxLength()
    {
        var input = new UserRegistration { Email = new string('a', 90) + "@example.com" };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public async Task FailWhenPasswordIsEmpty()
    {
        var input = new UserRegistration
        {
            Email = "user@example.com",
            Password = ""
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenPasswordIsTooShort()
    {
        var input = new UserRegistration
        {
            Email = "user@example.com",
            Password = "short"
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage(ValidationError.Short);
    }

    [Fact]
    public async Task FailWhenPasswordIsTooLong()
    {
        var input = new UserRegistration
        {
            Email = "user@example.com",
            Password = new string('a', 129)
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public async Task FailWhenAcceptedRulesIsFalse()
    {
        var input = new UserRegistration
        {
            Email = "user@example.com",
            Password = "password123",
            AcceptedRules = false
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.AcceptedRules);
    }
}
