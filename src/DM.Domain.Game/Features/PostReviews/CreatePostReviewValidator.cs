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
            .WithMessage("Укажите пост");

        RuleFor(r => r.Sign)
            .IsInEnum()
            .WithMessage("Оценка указана неверно");

        RuleFor(r => r.Text)
            .NotEmpty()
            .WithMessage("Введите текст рецензии");
    }
}
