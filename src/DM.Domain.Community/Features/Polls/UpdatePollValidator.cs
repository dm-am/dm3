using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Abstractions;
using FluentValidation;

namespace DM.Domain.Community.Features.Polls;

/// <inheritdoc />
internal class UpdatePollValidator : AbstractValidator<UpdatePoll>
{
    /// <inheritdoc />
    public UpdatePollValidator(
        IDateTimeProvider dateTimeProvider)
    {
        RuleFor(p => p.Id)
            .NotEmpty();
        RuleFor(p => p.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .When(p => p.Title != null);
        RuleFor(p => p.Details)
            .MaximumLength(1000).WithMessage(ValidationError.Long)
            .When(p => p.Details != null);
        RuleFor(p => p.StartsUtc)
            .GreaterThanOrEqualTo(dateTimeProvider.Now).WithMessage(ValidationError.Short)
            .When(p => p.StartsUtc.HasValue);
        RuleFor(p => p.EndsUtc)
            .GreaterThan(dateTimeProvider.Now).WithMessage(ValidationError.Short)
            .When(p => p.EndsUtc.HasValue);
    }
}
