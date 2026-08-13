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
            .MaximumLength(5000)
            .When(x => !string.IsNullOrEmpty(x.Text))
            .WithMessage("Рецензия не длиннее 5000 символов");
    }
}
