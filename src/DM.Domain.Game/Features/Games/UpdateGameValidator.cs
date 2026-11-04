using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Game.Features.Games;

/// <inheritdoc />
internal class UpdateGameValidator : AbstractValidator<UpdateGame>
{
    public UpdateGameValidator()
    {
        When(g => g.Title is not null, () =>
            RuleFor(g => g.Title)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(GameFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long));

        When(g => g.SystemName is not null, () =>
            RuleFor(g => g.SystemName)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(GameFieldLimits.SystemMaxLength).WithMessage(ValidationError.Long));

        When(g => g.NarrativeSetting is not null, () =>
            RuleFor(g => g.NarrativeSetting)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(GameFieldLimits.SettingMaxLength).WithMessage(ValidationError.Long));

        When(g => g.Info is not null, () =>
        {
            RuleFor(g => g.Info)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MinimumLength(GameFieldLimits.InfoMinLength).WithMessage(ValidationError.Short);

            // The Profile surface declares no [private].
            RuleFor(g => g.Info)
                .Must(info => !PrivateBlockMarkup.ContainsPrivateMarkup(info))
                .WithMessage(g => PrivateBlockMarkup.DescribeSurfaceRefusal(g.Info));
        });

        // An integer outside the enum matches no mode the product reads; null keeps the stored one.
        RuleFor(g => g.CommentsAccessMode)
            .IsInEnum().WithMessage(ValidationError.Invalid);
    }
}
