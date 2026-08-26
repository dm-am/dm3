using FluentValidation;

namespace DM.Domain.Community.Features.UserEndorsements;

/// <summary>
/// Validator for UpdateUserEndorsement
/// </summary>
internal class UpdateUserEndorsementValidator : AbstractValidator<UpdateUserEndorsement>
{
    public UpdateUserEndorsementValidator()
    {
        RuleFor(x => x.EndorsementId)
            .NotEmpty()
            .WithMessage("Укажите рекомендацию");

        RuleFor(x => x.Text)
            .MaximumLength(UserEndorsementFieldLimits.TextMaxLength)
            .When(x => !string.IsNullOrEmpty(x.Text))
            .WithMessage("Рекомендация не длиннее 5000 символов");
    }
}
