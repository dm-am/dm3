using FluentValidation;

namespace DM.Domain.Game.Features.Reviews;

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
    }
}
