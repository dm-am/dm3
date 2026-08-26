using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <summary>
/// Validator for chat event modification DTO model.
/// </summary>
/// <remarks>
/// The edit path had no validator at all, so every rule the create path states
/// held only until the author pressed edit. Each rule below is the create rule
/// under a null check, because null here means "keep what is stored" rather
/// than "clear it".
/// </remarks>
internal class UpdateGlobalChatEventValidator : AbstractValidator<UpdateGlobalChatEvent>
{
    /// <inheritdoc />
    public UpdateGlobalChatEventValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(e => e.Id)
            .NotEmpty().WithMessage(ValidationError.Empty);

        When(e => e.Title != null, () =>
            RuleFor(e => e.Title)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(GlobalChatEventFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long));

        When(e => e.Description != null, () =>
        {
            RuleFor(e => e.Description)
                .MaximumLength(GlobalChatEventFieldLimits.DescriptionMaxLength).WithMessage(ValidationError.Long);

            // The description renders on the Comment surface, which does not
            // declare [private]: the tag is not markup there and hides nothing.
            RuleFor(e => e.Description)
                .Must(description => !PrivateBlockMarkup.ContainsPrivateMarkup(description))
                .WithMessage(e => PrivateBlockMarkup.DescribeSurfaceRefusal(e.Description));
        });

        When(e => e.StartsUtc.HasValue, () =>
            RuleFor(e => e.StartsUtc!.Value)
                .Must(startsAt => startsAt > dateTimeProvider.Now)
                .WithMessage(ValidationError.MustBeFuture));

        RuleFor(e => e.Duration)
            .Must(duration => !duration.HasValue || duration.Value > TimeSpan.Zero)
            .WithMessage(ValidationError.MustBePositive);
    }
}
