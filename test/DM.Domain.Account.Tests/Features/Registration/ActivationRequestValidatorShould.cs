using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Registration;

public class ActivationRequestValidatorShould : UnitTestBase
{
    private readonly ActivationRequestValidator validator;
    private readonly Mock<IRegistrationRepository> repository;

    public ActivationRequestValidatorShould()
    {
        repository = Mock<IRegistrationRepository>();
        repository.Setup(r => r.UsernameFree(It.IsAny<string>(), default))
            .ReturnsAsync(true);

        validator = new ActivationRequestValidator(repository.Object);
    }

    [Fact]
    public async Task PassForValidInput()
    {
        var input = new ActivationRequest
        {
            Token = Guid.NewGuid(),
            Username = "ValidUser123"
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task FailWhenTokenIsEmpty()
    {
        var input = new ActivationRequest { Token = Guid.Empty, Username = "ValidUser" };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Token)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenUsernameIsEmpty()
    {
        var input = new ActivationRequest
        {
            Token = Guid.NewGuid(),
            Username = ""
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public async Task FailWhenUsernameContainsForbiddenCharacters()
    {
        var input = new ActivationRequest
        {
            Token = Guid.NewGuid(),
            Username = "User<Script>"
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public async Task FailWhenUsernameIsTooShort()
    {
        var input = new ActivationRequest
        {
            Token = Guid.NewGuid(),
            Username = "U"
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public async Task FailWhenUsernameIsTooLong()
    {
        var input = new ActivationRequest
        {
            Token = Guid.NewGuid(),
            Username = new string('a', 21)
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public async Task FailWhenUsernameStartsWithWhitespace()
    {
        var input = new ActivationRequest
        {
            Token = Guid.NewGuid(),
            Username = " Username"
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public async Task FailWhenUsernameEndsWithWhitespace()
    {
        var input = new ActivationRequest
        {
            Token = Guid.NewGuid(),
            Username = "Username "
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public async Task PassForUsernameWithSingleSpaces()
    {
        var input = new ActivationRequest
        {
            Token = Guid.NewGuid(),
            Username = "Valid Username"
        };
        var result = await validator.TestValidateAsync(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
