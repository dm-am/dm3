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
            .WithMessage("Target user ID is required");

        RuleFor(x => x.Text)
            .NotEmpty()
            .WithMessage("Endorsement text is required")
            .MaximumLength(5000)
            .WithMessage("Endorsement text must not exceed 5000 characters");
    }
}
