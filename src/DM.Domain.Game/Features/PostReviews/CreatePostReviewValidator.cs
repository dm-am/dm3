using DM.Domain.Core.Content;
using FluentValidation;

namespace DM.Domain.Game.Features.PostReviews;

internal class CreatePostReviewValidator : AbstractValidator<CreatePostReview>
{
    public CreatePostReviewValidator()
    {
        RuleFor(r => r.PostId)
            .NotEmpty()
            .WithMessage("Укажите пост");

        RuleFor(r => r.Sign)
            .IsInEnum()
            .WithMessage("Оценка указана неверно");

        RuleFor(r => r.Text)
            .NotEmpty()
            .WithMessage("Введите текст оценки");

        // The Comment surface does not declare [private].
        RuleFor(r => r.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .WithMessage(r => PrivateBlockMarkup.DescribeSurfaceRefusal(r.Text));
    }
}
