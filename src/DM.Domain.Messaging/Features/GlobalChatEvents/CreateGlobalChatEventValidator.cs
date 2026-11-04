using System;
using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Abstractions;
using FluentValidation;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <inheritdoc />
internal class CreateGlobalChatEventValidator : AbstractValidator<CreateGlobalChatEvent>
{
    public CreateGlobalChatEventValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(e => e.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(GlobalChatEventFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long);

        RuleFor(e => e.Description)
            .MaximumLength(GlobalChatEventFieldLimits.DescriptionMaxLength).WithMessage(ValidationError.Long);

        // The Comment surface does not declare [private]: the tag would hide nothing there.
        RuleFor(e => e.Description)
            .Must(description => !PrivateBlockMarkup.ContainsPrivateMarkup(description))
            .WithMessage(e => PrivateBlockMarkup.DescribeSurfaceRefusal(e.Description));

        RuleFor(e => e.StartsUtc)
            .Must(startsAt => startsAt > dateTimeProvider.Now)
            .WithMessage(ValidationError.MustBeFuture);

        RuleFor(e => e.Duration)
            .Must(duration => !duration.HasValue || duration.Value > TimeSpan.Zero)
            .WithMessage(ValidationError.MustBePositive);
    }
}
