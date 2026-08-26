using DM.Domain.Core.Content;
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
            .WithMessage("Укажите рецензию");

        // The same rule the create path states, and for the same reason: the
        // sign is added to a public rating as a number, so a value outside the
        // enum is an arbitrary amount of somebody else's rating. Omitted means
        // "keep what is stored", and IsInEnum passes a null through.
        RuleFor(r => r.Sign)
            .IsInEnum()
            .WithMessage("Оценка указана неверно");

        // Omitted means "keep what is stored"; present and blank means the
        // author is trying to publish a review with no text, which the create
        // path refuses too.
        RuleFor(r => r.Text)
            .NotEmpty()
            .When(r => r.Text != null)
            .WithMessage("Введите текст рецензии");

        // The Comment surface does not declare [private], and an edit is the
        // other way the tag gets into a stored review.
        RuleFor(r => r.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .When(r => r.Text != null)
            .WithMessage(r => PrivateBlockMarkup.DescribeSurfaceRefusal(r.Text));
    }
}
