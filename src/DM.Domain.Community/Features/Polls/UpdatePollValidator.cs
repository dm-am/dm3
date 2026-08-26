using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Community.Features.Polls;

/// <inheritdoc />
internal class UpdatePollValidator : AbstractValidator<UpdatePoll>
{
    /// <inheritdoc />
    public UpdatePollValidator()
    {
        RuleFor(p => p.Id)
            .NotEmpty();
        RuleFor(p => p.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .When(p => p.Title != null);
        RuleFor(p => p.Details)
            .MaximumLength(PollFieldLimits.DetailsMaxLength).WithMessage(ValidationError.Long)
            .When(p => p.Details != null);
        // Deliberately no "must be in the future" rules here, unlike creation:
        // the moderator edit form sends the dates of the poll being edited, so
        // such a rule would make every running poll uneditable. What an update
        // must still guarantee is that the poll does not end before it starts.
        RuleFor(p => p.EndsUtc)
            .GreaterThan(p => p.StartsUtc!.Value).WithMessage(ValidationError.Invalid)
            .When(p => p.StartsUtc.HasValue && p.EndsUtc.HasValue);
    }
}
