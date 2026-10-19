using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Abstractions;
using FluentValidation;

namespace DM.Domain.Community.Features.Polls;

/// <inheritdoc />
internal class CreatePollValidator : AbstractValidator<CreatePoll>
{
    // A minute, the step of the form's start field: a start less than one step behind this clock means now.
    private static readonly TimeSpan StartLeeway = TimeSpan.FromMinutes(1);

    public CreatePollValidator(
        IDateTimeProvider dateTimeProvider)
    {
        RuleFor(p => p.Title)
            .NotEmpty().WithMessage(ValidationError.Empty);
        RuleFor(p => p.Details)
            .MaximumLength(PollFieldLimits.DetailsMaxLength).WithMessage(ValidationError.Long)
            .When(p => p.Details != null);
        // PollFactory records a start let in from behind this clock as now.
        RuleFor(p => p.StartsUtc)
            .GreaterThan(dateTimeProvider.Now - StartLeeway).WithMessage(ValidationError.Short);
        RuleFor(p => p.EndsUtc)
            .GreaterThan(p => p.StartsUtc + TimeSpan.FromDays(1)).WithMessage(ValidationError.Short)
            .LessThan(dateTimeProvider.Now + TimeSpan.FromDays(365)).WithMessage(ValidationError.Long);
        RuleFor(p => p.Options)
            .NotEmpty().WithMessage(ValidationError.Empty);
        RuleForEach(p => p.Options)
            .NotEmpty().WithMessage(ValidationError.Empty);
    }
}
