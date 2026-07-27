using System;
using DM.Domain.Community.Features.UserEndorsements;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Community.Tests.Features.UserEndorsements;

public class UpdateUserEndorsementValidatorShould : UnitTestBase
{
    private readonly UpdateUserEndorsementValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdateUserEndorsement
        {
            EndorsementId = Guid.NewGuid(),
            Text = "Updated endorsement text"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenEndorsementIdIsEmpty()
    {
        var input = new UpdateUserEndorsement
        {
            EndorsementId = Guid.Empty,
            Text = "Updated endorsement text"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.EndorsementId);
    }

    [Fact]
    public void PassWhenTextIsNull()
    {
        var input = new UpdateUserEndorsement
        {
            EndorsementId = Guid.NewGuid(),
            Text = null
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTextIsTooLong()
    {
        var input = new UpdateUserEndorsement
        {
            EndorsementId = Guid.NewGuid(),
            Text = new string('a', 5001)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text);
    }

    [Fact]
    public void PassWhenTextIsAtMaxLength()
    {
        var input = new UpdateUserEndorsement
        {
            EndorsementId = Guid.NewGuid(),
            Text = new string('a', 5000)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
