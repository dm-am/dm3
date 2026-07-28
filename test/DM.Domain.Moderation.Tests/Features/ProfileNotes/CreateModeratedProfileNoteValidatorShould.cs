using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.ProfileNotes;
using DM.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.ProfileNotes;

public class CreateModeratedProfileNoteValidatorShould : UnitTestBase
{
    private readonly CreateModeratedProfileNoteValidator validator;

    public CreateModeratedProfileNoteValidatorShould()
    {
        validator = new CreateModeratedProfileNoteValidator();
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateModeratedProfileNote
        {
            Username = "testuser",
            Text = "Valid note text"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenUsernameIsEmpty()
    {
        var input = new CreateModeratedProfileNote
        {
            Username = "",
            Text = "Valid text"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenUsernameIsNull()
    {
        var input = new CreateModeratedProfileNote
        {
            Username = null!,
            Text = "Valid text"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void FailWhenTextIsEmpty()
    {
        var input = new CreateModeratedProfileNote
        {
            Username = "testuser",
            Text = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTextIsNull()
    {
        var input = new CreateModeratedProfileNote
        {
            Username = "testuser",
            Text = null!
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text);
    }

    [Fact]
    public void FailWhenTextExceedsMaxLength()
    {
        var input = new CreateModeratedProfileNote
        {
            Username = "testuser",
            Text = new string('x', 4001)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWithMaximumValidLength()
    {
        var input = new CreateModeratedProfileNote
        {
            Username = "testuser",
            Text = new string('x', 4000)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
