using System;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Creating;

/// <inheritdoc />
internal class CreateGlobalChatEventValidator : AbstractValidator<CreateGlobalChatEvent>
{
    /// <inheritdoc />
    public CreateGlobalChatEventValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(e => e.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(200).WithMessage(ValidationError.Long);

        RuleFor(e => e.Description)
            .MaximumLength(10000).WithMessage(ValidationError.Long);

        RuleFor(e => e.StartsAt)
            .Must(startsAt => startsAt > dateTimeProvider.Now)
            .WithMessage(ValidationError.MustBeFuture);

        RuleFor(e => e.Duration)
            .Must(duration => !duration.HasValue || duration.Value > TimeSpan.Zero)
            .WithMessage(ValidationError.MustBePositive);
    }
}
