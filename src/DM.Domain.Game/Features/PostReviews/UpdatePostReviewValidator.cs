using DM.Domain.Core.Content;
using FluentValidation;

namespace DM.Domain.Game.Features.PostReviews;

internal class UpdatePostReviewValidator : AbstractValidator<UpdatePostReview>
{
    public UpdatePostReviewValidator()
    {
        RuleFor(r => r.ReviewId)
            .NotEmpty()
            .WithMessage("Укажите оценку");

        // The sign is added to a public rating as a number; IsInEnum passes a null through.
        RuleFor(r => r.Sign)
            .IsInEnum()
            .WithMessage("Оценка указана неверно");

        // Omitted keeps the stored text; present and blank is refused, as on create.
        RuleFor(r => r.Text)
            .NotEmpty()
            .When(r => r.Text != null)
            .WithMessage("Введите текст оценки");

        // The Comment surface does not declare [private].
        RuleFor(r => r.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .When(r => r.Text != null)
            .WithMessage(r => PrivateBlockMarkup.DescribeSurfaceRefusal(r.Text));
    }
}
