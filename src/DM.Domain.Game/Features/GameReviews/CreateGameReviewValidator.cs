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
            .MaximumLength(5000)
            .WithMessage("Рецензия не длиннее 5000 символов");
    }
}
