using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Community.Features.PlatformReviews;

/// <summary>
/// Validator for UpdatePlatformReview
/// </summary>
internal class UpdatePlatformReviewValidator : AbstractValidator<UpdatePlatformReview>
{
    /// <inheritdoc />
    public UpdatePlatformReviewValidator()
    {
        RuleFor(r => r.Text)
            .MaximumLength(50000).WithMessage(ValidationError.Long)
            .When(r => r.Text != null);
    }
}
