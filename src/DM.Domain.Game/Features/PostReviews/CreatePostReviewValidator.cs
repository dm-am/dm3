using FluentValidation;

namespace DM.Domain.Game.Features.PostReviews;

/// <summary>
/// Validator for CreatePostReview input
/// </summary>
internal class CreatePostReviewValidator : AbstractValidator<CreatePostReview>
{
    public CreatePostReviewValidator()
    {
        RuleFor(r => r.PostId)
            .NotEmpty()
            .WithMessage("Post ID is required");

        RuleFor(r => r.Sign)
            .IsInEnum()
            .WithMessage("Invalid review sign");

        RuleFor(r => r.Text)
            .NotEmpty()
            .WithMessage("Review text is required");
    }
}
