using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Registration;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using FluentValidation.TestHelper;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Registration;

public class ActivationRequestValidatorShould : UnitTestBase
{
    private readonly ActivationRequestValidator validator;
    private readonly IRegistrationRepository repository;

    public ActivationRequestValidatorShould()
    {
        repository = Mock<IRegistrationRepository>();
        repository.UsernameFree(Arg.Any<string>(), default).Returns(true);

        validator = new ActivationRequestValidator(repository);
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

    /// <summary>
    /// Forbidden characters, one letter, twenty-one letters, a space at either end.
    /// </summary>
    [Theory]
    [InlineData("User<Script>")]
    [InlineData("U")]
    [InlineData("Abcdefghijklmnopqrstu")]
    [InlineData(" Username")]
    [InlineData("Username ")]
    public async Task FailWhenUsernameBreaksThePolicy(string username)
    {
        var input = new ActivationRequest
        {
            Token = Guid.NewGuid(),
            Username = username
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
