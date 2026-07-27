using System;
using DM.Domain.Community.Features.UserEndorsements;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Community.Tests.Features.UserEndorsements;

public class CreateUserEndorsementValidatorShould : UnitTestBase
{
    private readonly CreateUserEndorsementValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateUserEndorsement
        {
            TargetUserId = Guid.NewGuid(),
            Text = "Reliable game master"
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
        result.ShouldHaveValidationErrorFor(x => x.TargetUserId);
    }

    [Fact]
    public void FailWhenTextIsEmpty()
    {
        var input = new CreateUserEndorsement
        {
            TargetUserId = Guid.NewGuid(),
            Text = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text);
    }

    [Fact]
    public void FailWhenTextIsTooLong()
    {
        var input = new CreateUserEndorsement
        {
            TargetUserId = Guid.NewGuid(),
            Text = new string('a', 5001)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text);
    }

    [Fact]
    public void PassWhenTextIsAtMaxLength()
    {
        var input = new CreateUserEndorsement
        {
            TargetUserId = Guid.NewGuid(),
            Text = new string('a', 5000)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
