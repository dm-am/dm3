using FluentValidation;

namespace DM.Domain.Game.Features.Reviews;

/// <summary>
/// Validator for CreateGameReview
/// </summary>
internal class CreateGameReviewValidator : AbstractValidator<CreateGameReview>
{
    public CreateGameReviewValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("Game ID is required");

        RuleFor(x => x.Text)
            .NotEmpty()
            .WithMessage("Review text is required")
            .MaximumLength(5000)
            .WithMessage("Review text must not exceed 5000 characters");
    }
}
