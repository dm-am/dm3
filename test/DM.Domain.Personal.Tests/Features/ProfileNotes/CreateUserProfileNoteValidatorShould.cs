using DM.Domain.Core.Exceptions;
using DM.Domain.Personal.Features.ProfileNotes;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.ProfileNotes;

public class CreateUserProfileNoteValidatorShould : UnitTestBase
{
    private readonly CreateUserProfileNoteValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateUserProfileNote
        {
            SubjectUsername = "someone",
            Text = "Reliable game master"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenSubjectUsernameIsEmpty()
    {
        var input = new CreateUserProfileNote
        {
            SubjectUsername = "",
            Text = "Reliable game master"
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.SubjectUsername)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTextIsEmpty()
    {
        var input = new CreateUserProfileNote
        {
            SubjectUsername = "someone",
            Text = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTextExceedsMaxLength()
    {
        var input = new CreateUserProfileNote
        {
            SubjectUsername = "someone",
            Text = new string('a', 5001)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Long);
    }
}
