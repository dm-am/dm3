using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Tickets;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tickets;

public class CreateTicketValidatorShould : UnitTestBase
{
    private readonly CreateTicketValidator validator;

    public CreateTicketValidatorShould()
    {
        validator = new CreateTicketValidator();
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateTicket
        {
            TargetUsername = "testuser",
            Description = "Valid description",
            Comment = "Valid comment"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTargetUsernameIsEmpty()
    {
        var input = new CreateTicket
        {
            TargetUsername = "",
            Description = "Valid description",
            Comment = "Valid comment"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.TargetUsername)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenDescriptionIsEmpty()
    {
        var input = new CreateTicket
        {
            TargetUsername = "testuser",
            Description = "",
            Comment = "Valid comment"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenCommentIsEmpty()
    {
        var input = new CreateTicket
        {
            TargetUsername = "testuser",
            Description = "Valid description",
            Comment = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Comment)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenDescriptionExceedsMaxLength()
    {
        var input = new CreateTicket
        {
            TargetUsername = "testuser",
            Description = new string('x', 5001),
            Comment = "Valid comment"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void FailWhenCommentExceedsMaxLength()
    {
        var input = new CreateTicket
        {
            TargetUsername = "testuser",
            Description = "Valid description",
            Comment = new string('x', 2001)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Comment)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWithOptionalEntityType()
    {
        var input = new CreateTicket
        {
            TargetUsername = "testuser",
            Description = "Valid description",
            Comment = "Valid comment",
            EntityType = "Game"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenEntityTypeExceedsMaxLength()
    {
        var input = new CreateTicket
        {
            TargetUsername = "testuser",
            Description = "Valid description",
            Comment = "Valid comment",
            EntityType = new string('x', 101)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.EntityType)
            .WithErrorMessage(ValidationError.Long);
    }
}
