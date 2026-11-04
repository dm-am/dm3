using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Community.Features.Polls;

/// <inheritdoc />
internal class UpdatePollValidator : AbstractValidator<UpdatePoll>
{
    public UpdatePollValidator()
    {
        RuleFor(p => p.Id)
            .NotEmpty().WithMessage(ValidationError.Empty);
        RuleFor(p => p.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .When(p => p.Title != null);
        RuleFor(p => p.Details)
            .MaximumLength(PollFieldLimits.DetailsMaxLength).WithMessage(ValidationError.Long)
            .When(p => p.Details != null);
        // No "must be in the future" rules, unlike creation: the edit form sends the
        // dates of the poll being edited, and every running poll would be uneditable.
        RuleFor(p => p.EndsUtc)
            .GreaterThan(p => p.StartsUtc!.Value).WithMessage(ValidationError.Invalid)
            .When(p => p.StartsUtc.HasValue && p.EndsUtc.HasValue);
    }
}
