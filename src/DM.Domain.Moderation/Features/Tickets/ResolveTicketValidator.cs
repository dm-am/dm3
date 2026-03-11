using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// Validator for ticket resolution
/// </summary>
internal class ResolveTicketValidator : AbstractValidator<ResolveTicket>
{
    public ResolveTicketValidator()
    {
        RuleFor(r => r.Status)
            .Must(s => s != TicketStatus.Open && s != TicketStatus.InProgress)
            .WithMessage("Status must be a resolution status (Resolved, Rejected, etc.)");

        RuleFor(r => r.Answer)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(2000).WithMessage(ValidationError.Long);

        // Warning validation
        RuleFor(r => r.WarningPoints)
            .InclusiveBetween(1, 3).WithMessage(ValidationError.Invalid)
            .When(r => r.IssueWarning);

        RuleFor(r => r.WarningText)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(2000).WithMessage(ValidationError.Long)
            .When(r => r.IssueWarning);

        // Ban validation
        RuleFor(r => r.BanDurationHours)
            .NotNull().WithMessage(ValidationError.Empty)
            .GreaterThan(0).WithMessage(ValidationError.Invalid)
            .When(r => r.IssueBan);

        RuleFor(r => r.BanComment)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(2000).WithMessage(ValidationError.Long)
            .When(r => r.IssueBan);
    }
}
