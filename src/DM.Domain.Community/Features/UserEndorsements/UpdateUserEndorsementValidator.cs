using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Community.Features.UserEndorsements;

/// <inheritdoc />
internal class UpdateUserEndorsementValidator : AbstractValidator<UpdateUserEndorsement>
{
    public UpdateUserEndorsementValidator()
    {
        RuleFor(x => x.EndorsementId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        // Omitted keeps the stored text; present and blank is refused, as on create.
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(UserEndorsementFieldLimits.TextMaxLength).WithMessage(ValidationError.Long)
            .When(x => x.Text != null);
    }
}
