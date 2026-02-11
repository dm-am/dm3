using System;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Polls.Creating;

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