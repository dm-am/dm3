using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Domain.Core.Exceptions;
using DM.Testing;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Domain.Community.Tests.Features.WebsiteTestimonials;

public class CreateWebsiteTestimonialValidatorShould : UnitTestBase
{
    private readonly CreateWebsiteTestimonialValidator validator = new();

    [Fact]
    public void PassForValidInput()
    {
        var input = new CreateWebsiteTestimonial
        {
            Text = "Great place for text roleplay"
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenTextIsEmpty()
    {
        var input = new CreateWebsiteTestimonial
        {
            Text = ""
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Empty);
    }

    [Fact]
    public void FailWhenTextIsTooLong()
    {
        var input = new CreateWebsiteTestimonial
        {
            Text = new string('a', 10001)
        };

        var result = validator.TestValidate(input);
        result.ShouldHaveValidationErrorFor(x => x.Text)
            .WithErrorMessage(ValidationError.Long);
    }

    [Fact]
    public void PassWhenTextIsAtMaxLength()
    {
        var input = new CreateWebsiteTestimonial
        {
            Text = new string('a', 10000)
        };

        var result = validator.TestValidate(input);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
