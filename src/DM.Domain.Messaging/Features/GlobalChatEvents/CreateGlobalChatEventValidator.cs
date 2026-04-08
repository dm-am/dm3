using System;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Abstractions;
using FluentValidation;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

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

        RuleFor(e => e.StartsUtc)
            .Must(startsAt => startsAt > dateTimeProvider.Now)
            .WithMessage(ValidationError.MustBeFuture);

        RuleFor(e => e.Duration)
            .Must(duration => !duration.HasValue || duration.Value > TimeSpan.Zero)
            .WithMessage(ValidationError.MustBePositive);
    }
}
