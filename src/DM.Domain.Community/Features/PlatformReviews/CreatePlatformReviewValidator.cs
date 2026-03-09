using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Community.Features.PlatformReviews;

/// <summary>
/// Validator for CreatePlatformReview
/// </summary>
internal class CreatePlatformReviewValidator : AbstractValidator<CreatePlatformReview>
{
    /// <inheritdoc />
    public CreatePlatformReviewValidator()
    {
        RuleFor(r => r.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(50000).WithMessage(ValidationError.Long);
    }
}
