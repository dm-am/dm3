using DM.Domain.Core.Content;
using FluentValidation;

namespace DM.Domain.Game.Features.GameReviews;

internal class UpdateGameReviewValidator : AbstractValidator<UpdateGameReview>
{
    public UpdateGameReviewValidator()
    {
        RuleFor(x => x.ReviewId)
            .NotEmpty()
            .WithMessage("Укажите рецензию");

        // Omitted keeps the stored text; present and blank is refused, as on create.
        RuleFor(x => x.Text)
            .NotEmpty()
            .When(x => x.Text != null)
            .WithMessage("Введите текст рецензии");

        RuleFor(x => x.Text)
            .MaximumLength(GameReviewFieldLimits.TextMaxLength)
            .When(x => !string.IsNullOrEmpty(x.Text))
            .WithMessage($"Рецензия не длиннее {GameReviewFieldLimits.TextMaxLength} символов");

        // The Comment surface does not declare [private].
        RuleFor(x => x.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .When(x => !string.IsNullOrEmpty(x.Text))
            .WithMessage(x => PrivateBlockMarkup.DescribeSurfaceRefusal(x.Text));
    }
}
