using FluentValidation;

namespace DM.Domain.Community.Features.UserReviews;

/// <summary>
/// Validator for UpdateUserReview
/// </summary>
internal class UpdateUserReviewValidator : AbstractValidator<UpdateUserReview>
{
    public UpdateUserReviewValidator()
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
