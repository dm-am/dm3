using DM.Infrastructure.Mail;
using DM.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;
using DM.Domain.Core.Mail;

namespace DM.Infrastructure.Mail.Tests;

public class EmailLetterValidatorShould : UnitTestBase
{
    private readonly EmailLetterValidator validator;

    public EmailLetterValidatorShould()
    {
        validator = new EmailLetterValidator();
    }

    [Fact]
    public void PassForValidEmailLetter()
    {
        var letter = new EmailLetter
        {
            Address = "test@example.com",
            Subject = "Test Subject",
            Body = "This is a test email body"
        };

        var result = validator.TestValidate(letter);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenAddressIsEmpty()
    {
        var letter = new EmailLetter
        {
            Address = "",
            Subject = "Test Subject",
            Body = "Test body"
        };

        var result = validator.TestValidate(letter);
        result.ShouldHaveValidationErrorFor(l => l.Address);
    }

    [Fact]
    public void FailWhenAddressIsInvalid()
    {
        var letter = new EmailLetter
        {
            Address = "not-an-email",
            Subject = "Test Subject",
            Body = "Test body"
        };

        var result = validator.TestValidate(letter);
        result.ShouldHaveValidationErrorFor(l => l.Address);
    }

    [Fact]
    public void FailWhenSubjectIsEmpty()
    {
        var letter = new EmailLetter
        {
            Address = "test@example.com",
            Subject = "",
            Body = "Test body"
        };

        var result = validator.TestValidate(letter);
        result.ShouldHaveValidationErrorFor(l => l.Subject);
    }

    [Fact]
    public void FailWhenSubjectExceedsMaxLength()
    {
        var letter = new EmailLetter
        {
            Address = "test@example.com",
            Subject = new string('a', 101),
            Body = "Test body"
        };

        var result = validator.TestValidate(letter);
        result.ShouldHaveValidationErrorFor(l => l.Subject);
    }

    [Fact]
    public void PassWhenSubjectIsAtMaxLength()
    {
        var letter = new EmailLetter
        {
            Address = "test@example.com",
            Subject = new string('a', 100),
            Body = "Test body"
        };

        var result = validator.TestValidate(letter);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void FailWhenBodyIsEmpty()
    {
        var letter = new EmailLetter
        {
            Address = "test@example.com",
            Subject = "Test Subject",
            Body = ""
        };

        var result = validator.TestValidate(letter);
        result.ShouldHaveValidationErrorFor(l => l.Body);
    }

    [Fact]
    public void PassWithMinimalValidInput()
    {
        var letter = new EmailLetter
        {
            Address = "a@b.c",
            Subject = "A",
            Body = "B"
        };

        var result = validator.TestValidate(letter);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
