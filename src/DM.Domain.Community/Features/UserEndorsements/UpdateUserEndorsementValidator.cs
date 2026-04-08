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
            .WithMessage("Endorsement ID is required");

        RuleFor(x => x.Text)
            .MaximumLength(5000)
            .When(x => !string.IsNullOrEmpty(x.Text))
            .WithMessage("Endorsement text must not exceed 5000 characters");
    }
}
