using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// Validator for ticket creation
/// </summary>
internal class CreateTicketValidator : AbstractValidator<CreateTicket>
{
    public CreateTicketValidator()
    {
        RuleFor(t => t.TargetUsername)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(t => t.Description)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(5000).WithMessage(ValidationError.Long);

        RuleFor(t => t.Comment)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(2000).WithMessage(ValidationError.Long);

        RuleFor(t => t.EntityType)
            .MaximumLength(100).WithMessage(ValidationError.Long)
            .When(t => t.EntityType != null);
    }
}
