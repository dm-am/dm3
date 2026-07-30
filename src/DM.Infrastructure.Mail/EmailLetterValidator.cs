using DM.Domain.Core.Mail;
using FluentValidation;

namespace DM.Infrastructure.Mail;

/// <inheritdoc />
internal class EmailLetterValidator : AbstractValidator<EmailLetter>
{
    /// <inheritdoc />
    public EmailLetterValidator()
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
