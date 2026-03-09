using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Abstractions;
using FluentValidation;

namespace DM.Domain.Community.Features.Polls;

/// <inheritdoc />
internal class CreatePollValidator : AbstractValidator<CreatePoll>
{
    /// <inheritdoc />
    public CreatePollValidator(
        IDateTimeProvider dateTimeProvider)
    {
        RuleFor(p => p.Title)
            .NotEmpty().WithMessage(ValidationError.Empty);
        RuleFor(p => p.EndDate)
            .GreaterThan(dateTimeProvider.Now + TimeSpan.FromDays(1)).WithMessage(ValidationError.Short)
            .LessThan(dateTimeProvider.Now + TimeSpan.FromDays(365)).WithMessage(ValidationError.Long);
        RuleFor(p => p.Options)
            .NotEmpty().WithMessage(ValidationError.Empty);
        RuleForEach(p => p.Options)
            .NotEmpty().WithMessage(ValidationError.Empty);
    }
}