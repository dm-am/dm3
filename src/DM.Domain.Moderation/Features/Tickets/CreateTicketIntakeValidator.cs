using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// Validator for ticket creation from the public intake forms
/// </summary>
internal class CreateTicketIntakeValidator : AbstractValidator<CreateTicketIntake>
{
    public CreateTicketIntakeValidator(IIdentityProvider identityProvider)
    {
        RuleFor(t => t.Subtype)
            .IsInEnum().WithMessage(ValidationError.Invalid);

        RuleFor(t => t.Subject)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(200).WithMessage(ValidationError.Long);

        RuleFor(t => t.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(10000).WithMessage(ValidationError.Long);

        // Guests have no account to reach them by, so a contact email is
        // required and must be a valid address — without it the moderation
        // response and the tracking flow have nowhere to land. Authenticated
        // authors are reachable by identity, so their contact stays optional.
        When(_ => !identityProvider.Current.User.IsAuthenticated, () =>
        {
            RuleFor(t => t.Contact)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .EmailAddress().WithMessage(ValidationError.Invalid)
                .MaximumLength(200).WithMessage(ValidationError.Long);
        }).Otherwise(() =>
        {
            RuleFor(t => t.Contact)
                .MaximumLength(200).WithMessage(ValidationError.Long)
                .When(t => t.Contact != null);
        });

        RuleFor(t => t.ViolationUrl)
            .MaximumLength(500).WithMessage(ValidationError.Long)
            .When(t => t.ViolationUrl != null);
    }
}
