using FluentValidation;

namespace DM.Domain.Game.Features.PostReviews;

/// <summary>
/// Validator for UpdatePostReview input
/// </summary>
internal class UpdatePostReviewValidator : AbstractValidator<UpdatePostReview>
{
    public UpdatePostReviewValidator()
    {
        RuleFor(r => r.ReviewId)
            .NotEmpty()
            .WithMessage("Review ID is required");
    }
}
