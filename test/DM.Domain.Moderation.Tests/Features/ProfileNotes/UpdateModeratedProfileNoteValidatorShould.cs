using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.ProfileNotes;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.ProfileNotes;

public class UpdateModeratedProfileNoteValidatorShould : UnitTestBase
{
    private readonly UpdateModeratedProfileNoteValidator validator;

    public UpdateModeratedProfileNoteValidatorShould()
    {
        validator = new UpdateModeratedProfileNoteValidator();
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new UpdateModeratedProfileNote
        {
            Id = Guid.NewGuid(),
            Text = "Updated note text"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenNoteIdIsEmpty()
    {
        var input = new UpdateModeratedProfileNote
        {
            Id = Guid.Empty,
            Text = "Valid text"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Id)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTextIsEmpty()
    {
        var input = new UpdateModeratedProfileNote
        {
            Id = Guid.NewGuid(),
            Text = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTextIsNull()
    {
        var input = new UpdateModeratedProfileNote
        {
            Id = Guid.NewGuid(),
            Text = null!
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text);
    }

    [Fact]
    public void FailWhenTextExceedsMaxLength()
    {
        var input = new UpdateModeratedProfileNote
        {
            Id = Guid.NewGuid(),
            Text = new string('x', 4001)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWithMaximumValidLength()
    {
        var input = new UpdateModeratedProfileNote
        {
            Id = Guid.NewGuid(),
            Text = new string('x', 4000)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
