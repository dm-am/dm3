using DM.Domain.Core.Content;
using FluentValidation;

namespace DM.Domain.Game.Features.GameReviews;

/// <summary>
/// Validator for CreateGameReview
/// </summary>
internal class CreateGameReviewValidator : AbstractValidator<CreateGameReview>
{
    public CreateGameReviewValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("Укажите игру");

        RuleFor(x => x.Text)
            .NotEmpty()
            .WithMessage("Введите текст рецензии")
            .MaximumLength(GameReviewFieldLimits.TextMaxLength)
            .WithMessage("Рецензия не длиннее 5000 символов");

        // A review renders on the Comment surface, which does not declare
        // [private]: the tag is not markup there and hides nothing.
        RuleFor(x => x.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .WithMessage(x => PrivateBlockMarkup.DescribeSurfaceRefusal(x.Text));
    }
}
