using DM.Domain.Account.Features.UsernameChange;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Account.Tests.Features.UsernameChange;

public class CreateUsernameChangeRequestValidatorShould : UnitTestBase
{
    private readonly CreateUsernameChangeRequestValidator validator;

    public CreateUsernameChangeRequestValidatorShould()
    {
        validator = new CreateUsernameChangeRequestValidator();
    }

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateUsernameChangeRequest
        {
            Reason = "I need to change my username because of privacy concerns"
        };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenReasonIsEmpty()
    {
        var input = new CreateUsernameChangeRequest { Reason = "" };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Reason)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenReasonExceedsMaxLength()
    {
        var input = new CreateUsernameChangeRequest { Reason = new string('a', 501) };
        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Reason)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWhenReasonIsAtMaxLength()
    {
        var input = new CreateUsernameChangeRequest { Reason = new string('a', 500) };
        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
