using System.Text.RegularExpressions;
using DM.Domain.Core.Exceptions;
using FluentValidation;
using DM.Domain.Core.Users;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Validator for activation request (username selection during activation)
/// </summary>
internal partial class ActivationRequestValidator : AbstractValidator<ActivationRequest>
{
    // Forbidden: control chars, HTML/URL unsafe, quotes, brackets, special chars, zero-width
    // Whitespace: not at start/end, not consecutive
    // See: docs/conventions/USERNAME_POLICY.md
    [GeneratedRegex(UsernamePolicy.Pattern)]
    private static partial Regex UsernameValidationRegex();

    public ActivationRequestValidator(IRegistrationRepository registrationRepository)
    {
        RuleFor(r => r.Token)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(r => r.Username)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .Must(username => UsernameValidationRegex().IsMatch(username))
            .WithMessage(ValidationError.Invalid)
            .MustAsync(registrationRepository.UsernameFree).WithMessage(ValidationError.Taken);
    }
}
