using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <summary>
/// The rules of creation, each under a null check: null keeps what is stored.
/// </summary>
internal class UpdateGlobalChatEventValidator : AbstractValidator<UpdateGlobalChatEvent>
{
    public UpdateGlobalChatEventValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(e => e.Id)
            .NotEmpty().WithMessage(ValidationError.Empty);

        When(e => e.Title != null, () =>
            RuleFor(e => e.Title)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(GlobalChatEventFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long));

        RuleFor(e => e.Description)
            .MaximumLength(GlobalChatEventFieldLimits.DescriptionMaxLength).WithMessage(ValidationError.Long);

        // The Comment surface does not declare [private]: the tag would hide nothing there.
        RuleFor(e => e.Description)
            .Must(description => !PrivateBlockMarkup.ContainsPrivateMarkup(description))
            .WithMessage(e => PrivateBlockMarkup.DescribeSurfaceRefusal(e.Description));

        When(e => e.StartsUtc.HasValue, () =>
            RuleFor(e => e.StartsUtc!.Value)
                .Must(startsAt => startsAt > dateTimeProvider.Now)
                .WithMessage(ValidationError.MustBeFuture));

        RuleFor(e => e.Duration)
            .Must(duration => !duration.HasValue || duration.Value > TimeSpan.Zero)
            .WithMessage(ValidationError.MustBePositive);
    }
}
