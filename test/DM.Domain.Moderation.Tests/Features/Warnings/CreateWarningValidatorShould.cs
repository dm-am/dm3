using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Warnings;
using DM.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Warnings;

public class CreateWarningValidatorShould : UnitTestBase
{
    private readonly CreateWarningValidator validator;

    public CreateWarningValidatorShould()
    {
        validator = new CreateWarningValidator();
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateWarning
        {
            Username = "testuser",
            Points = 2,
            Reason = "Valid warning reason"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenUsernameIsEmpty()
    {
        var input = new CreateWarning
        {
            Username = "",
            Points = 2,
            Reason = "Valid reason"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenPointsAreTooLow()
    {
        var input = new CreateWarning
        {
            Username = "testuser",
            Points = -1,
            Reason = "Valid reason"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Points)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void FailWhenPointsAreTooHigh()
    {
        var input = new CreateWarning
        {
            Username = "testuser",
            Points = 7,
            Reason = "Valid reason"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Points)
            .WithErrorMessage(ValidationError.Invalid);
    }

    [Fact]
    public void PassForAllValidPointValues()
    {
        // 0 = verbal warning without points, 6 = maximum single-warning severity
        for (int points = 0; points <= 6; points++)
        {
            var input = new CreateWarning
            {
                Username = "testuser",
                Points = points,
                Reason = "Valid reason"
            };

            var result = validator.TestValidate(input);
            result.ShouldNotHaveAnyValidationErrors();
        }
    }

    [Fact]
    public void FailWhenReasonIsEmpty()
    {
        var input = new CreateWarning
        {
            Username = "testuser",
            Points = 2,
            Reason = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Reason)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenReasonExceedsMaxLength()
    {
        var input = new CreateWarning
        {
            Username = "testuser",
            Points = 2,
            Reason = new string('x', 2001)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Reason)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWithOptionalEntityType()
    {
        var input = new CreateWarning
        {
            Username = "testuser",
            Points = 2,
            Reason = "Valid reason",
            EntityType = "Game"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenEntityTypeExceedsMaxLength()
    {
        var input = new CreateWarning
        {
            Username = "testuser",
            Points = 2,
            Reason = "Valid reason",
            EntityType = new string('x', 101)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.EntityType)
            .WithErrorMessage(ValidationError.Long);
    }
}
