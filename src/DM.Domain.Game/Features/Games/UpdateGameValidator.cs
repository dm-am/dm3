using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Game.Features.Games;

/// <inheritdoc />
internal class UpdateGameValidator : AbstractValidator<UpdateGame>
{
    /// <inheritdoc />
    public UpdateGameValidator()
    {
        When(g => g.Title != default, () =>
            RuleFor(g => g.Title)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(GameFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long));

        When(g => g.SystemName != default, () =>
            RuleFor(g => g.SystemName)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(GameFieldLimits.SystemMaxLength).WithMessage(ValidationError.Long));

        When(g => g.NarrativeSetting != default, () =>
            RuleFor(g => g.NarrativeSetting)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(GameFieldLimits.SettingMaxLength).WithMessage(ValidationError.Long));

        When(g => g.Info != default, () =>
        {
            RuleFor(g => g.Info)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MinimumLength(GameFieldLimits.InfoMinLength).WithMessage(ValidationError.Short);

            // The Profile surface declares neither [mod] nor [private], and an
            // edit is the other way the tag gets into a stored description.
            RuleFor(g => g.Info)
                .Must(info => !PrivateBlockMarkup.ContainsPrivateMarkup(info))
                .WithMessage(g => PrivateBlockMarkup.DescribeSurfaceRefusal(g.Info));
        });

        // Who may comment is decided by comparing the stored value against the
        // named members; an integer outside the enum matches none of them and
        // the game ends up with an access mode nothing in the product can read.
        // Null passes — omitted keeps the stored value.
        RuleFor(g => g.CommentsAccessMode)
            .IsInEnum().WithMessage(ValidationError.Invalid);
    }
}
