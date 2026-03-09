using FluentValidation;

namespace DM.Domain.Community.Features.UserReviews;

/// <summary>
/// Validator for CreateUserReview
/// </summary>
internal class CreateUserReviewValidator : AbstractValidator<CreateUserReview>
{
    public CreateUserReviewValidator()
    {
        RuleFor(x => x.TargetUserId)
            .NotEmpty()
            .WithMessage("Target user ID is required");

        RuleFor(x => x.Text)
            .NotEmpty()
            .WithMessage("Review text is required")
            .MaximumLength(5000)
            .WithMessage("Review text must not exceed 5000 characters");
    }
}
