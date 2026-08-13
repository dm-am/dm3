using FluentValidation;

namespace DM.Domain.Community.Features.UserEndorsements;

/// <summary>
/// Validator for CreateUserEndorsement
/// </summary>
internal class CreateUserEndorsementValidator : AbstractValidator<CreateUserEndorsement>
{
    public CreateUserEndorsementValidator()
    {
        RuleFor(x => x.TargetUserId)
            .NotEmpty()
            .WithMessage("Укажите пользователя");

        RuleFor(x => x.Text)
            .NotEmpty()
            .WithMessage("Введите текст рекомендации")
            .MaximumLength(5000)
            .WithMessage("Рекомендация не длиннее 5000 символов");
    }
}
