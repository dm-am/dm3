using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Community.Features.UserEndorsements;

/// <inheritdoc />
internal class CreateUserEndorsementValidator : AbstractValidator<CreateUserEndorsement>
{
    public CreateUserEndorsementValidator()
    {
        RuleFor(x => x.TargetUserId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(x => x.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(UserEndorsementFieldLimits.TextMaxLength).WithMessage(ValidationError.Long);
    }
}
