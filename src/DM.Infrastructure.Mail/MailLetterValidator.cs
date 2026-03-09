using FluentValidation;

namespace DM.Infrastructure.Mail;

/// <inheritdoc />
internal class MailLetterValidator : AbstractValidator<MailLetter>
{
    /// <inheritdoc />
    public MailLetterValidator()
    {
        RuleFor(l => l.Address)
            .NotEmpty()
            .EmailAddress();
        RuleFor(l => l.Subject)
            .NotEmpty()
            .MaximumLength(100);
        RuleFor(l => l.Body)
            .NotEmpty();
    }
}
