using DM.Domain.Core.Content;
using FluentValidation;

namespace DM.Domain.Game.Features.GameReviews;

/// <summary>
/// Validator for UpdateGameReview
/// </summary>
internal class UpdateGameReviewValidator : AbstractValidator<UpdateGameReview>
{
    public UpdateGameReviewValidator()
    {
        RuleFor(x => x.ReviewId)
            .NotEmpty()
            .WithMessage("Укажите рецензию");

        RuleFor(x => x.Text)
            .MaximumLength(GameReviewFieldLimits.TextMaxLength)
            .When(x => !string.IsNullOrEmpty(x.Text))
            .WithMessage("Рецензия не длиннее 5000 символов");

        // The Comment surface does not declare [private], and an edit is the
        // other way the tag gets into a stored review.
        RuleFor(x => x.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .When(x => !string.IsNullOrEmpty(x.Text))
            .WithMessage(x => PrivateBlockMarkup.DescribeSurfaceRefusal(x.Text));
    }
}
