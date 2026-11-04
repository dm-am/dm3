using System;
using DM.Domain.Community.Features.UserEndorsements;
using DM.Domain.Core.Exceptions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Community.Tests.Features.UserEndorsements;

public class CreateUserEndorsementValidatorShould
{
    private readonly CreateUserEndorsementValidator validator = new();

    [Theory]
    [InlineData("Reliable game master")]
    [InlineData("Я")]
    public void PassForValidInput(string text)
    {
        var input = new CreateUserEndorsement
        {
            TargetUserId = Guid.NewGuid(),
            Text = text
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTargetUserIdIsEmpty()
    {
        var input = new CreateUserEndorsement
        {
            TargetUserId = Guid.Empty,
            Text = "Reliable game master"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.TargetUserId).WithErrorMessage(ValidationError.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FailWhenTextIsBlank(string text)
    {
        var input = new CreateUserEndorsement
        {
            TargetUserId = Guid.NewGuid(),
            Text = text
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text).WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTextIsTooLong()
    {
        var input = new CreateUserEndorsement
        {
            TargetUserId = Guid.NewGuid(),
            Text = new string('a', UserEndorsementFieldLimits.TextMaxLength + 1)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text).WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWhenTextIsAtMaxLength()
    {
        var input = new CreateUserEndorsement
        {
            TargetUserId = Guid.NewGuid(),
            Text = new string('a', UserEndorsementFieldLimits.TextMaxLength)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
