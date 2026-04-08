using DM.Infrastructure.Mail;
using DM.Testing;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace DM.Infrastructure.Mail.Tests;

public class MailLetterValidatorShould : UnitTestBase
{
    private readonly MailLetterValidator validator;

    public MailLetterValidatorShould()
    {
        validator = new MailLetterValidator();
    }

    [Fact]
    public void PassForValidMailLetter()
    {
        var letter = new MailLetter
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
        var letter = new MailLetter
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
        var letter = new MailLetter
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
        var letter = new MailLetter
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
        var letter = new MailLetter
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
        var letter = new MailLetter
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
        var letter = new MailLetter
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
        var letter = new MailLetter
        {
            Address = "a@b.c",
            Subject = "A",
            Body = "B"
        };

        var result = validator.TestValidate(letter);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
