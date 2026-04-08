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
            .WithMessage("Review ID is required");

        RuleFor(x => x.Text)
            .MaximumLength(5000)
            .When(x => !string.IsNullOrEmpty(x.Text))
            .WithMessage("Review text must not exceed 5000 characters");
    }
}
