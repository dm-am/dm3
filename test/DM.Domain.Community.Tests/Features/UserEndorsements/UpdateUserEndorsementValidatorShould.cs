using System;
using DM.Domain.Community.Features.UserEndorsements;
using DM.Domain.Core.Exceptions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Community.Tests.Features.UserEndorsements;

public class UpdateUserEndorsementValidatorShould
{
    private readonly UpdateUserEndorsementValidator validator = new();

    [Theory]
    [InlineData("Updated endorsement text")]
    [InlineData("Я")]
    public void PassForValidInput(string text)
    {
        var input = new UpdateUserEndorsement
        {
            EndorsementId = Guid.NewGuid(),
            Text = text
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
        result.ShouldHaveValidationErrorFor(x => x.EndorsementId).WithErrorMessage(ValidationError.Empty);
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
            Text = new string('a', UserEndorsementFieldLimits.TextMaxLength + 1)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text).WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWhenTextIsAtMaxLength()
    {
        var input = new UpdateUserEndorsement
        {
            EndorsementId = Guid.NewGuid(),
            Text = new string('a', UserEndorsementFieldLimits.TextMaxLength)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    /// <summary>
    /// Present and blank is not "keep what is stored": the service trims the text
    /// before storing it, so a text of spaces would store an empty recommendation.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FailWhenTextIsBlank(string text) =>
        validator.TestValidate(new UpdateUserEndorsement { EndorsementId = Guid.NewGuid(), Text = text })
            .ShouldHaveValidationErrorFor(x => x.Text).WithErrorMessage(ValidationError.Empty);
}
