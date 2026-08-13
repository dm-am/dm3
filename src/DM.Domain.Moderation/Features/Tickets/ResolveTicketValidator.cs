using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// Validator for ticket resolution
/// </summary>
internal class ResolveTicketValidator : AbstractValidator<ResolveTicket>
{
    /// <summary>
    /// Upper bound for a ticket-issued ban, in hours (~10 years). Ban issuance
    /// via ticket resolution is always time-boxed (permanent bans go through
    /// the dedicated ban endpoint); the bound also prevents a DateTimeOffset
    /// overflow when the duration is added to "now".
    /// </summary>
    private const int MaxBanDurationHours = 24 * 365 * 10;

    public ResolveTicketValidator()
    {
        RuleFor(r => r.Status)
            .Must(s => s is TicketStatus.Closed or TicketStatus.Spam)
            .WithMessage("Обращение можно закрыть или пометить спамом");

        RuleFor(r => r.Answer)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(2000).WithMessage(ValidationError.Long);

        // Warning validation: 0-6 points, matching the warning system clamp
        // (0 = verbal warning without points).
        RuleFor(r => r.WarningPoints)
            .InclusiveBetween(0, 6).WithMessage(ValidationError.Invalid)
            .When(r => r.IssueWarning);

        RuleFor(r => r.WarningText)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(2000).WithMessage(ValidationError.Long)
            .When(r => r.IssueWarning);

        // Ban validation: a positive, bounded duration.
        RuleFor(r => r.BanDurationHours)
            .NotNull().WithMessage(ValidationError.Empty)
            .GreaterThan(0).WithMessage(ValidationError.Invalid)
            .LessThanOrEqualTo(MaxBanDurationHours).WithMessage(ValidationError.Invalid)
            .When(r => r.IssueBan);

        RuleFor(r => r.BanComment)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(2000).WithMessage(ValidationError.Long)
            .When(r => r.IssueBan);
    }
}
