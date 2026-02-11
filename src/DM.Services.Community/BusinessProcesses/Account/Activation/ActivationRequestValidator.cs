using System.Text.RegularExpressions;
using DM.Services.Community.BusinessProcesses.Account.Registration;
using DM.Services.Core.Exceptions;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Account.Activation;

/// <summary>
/// Validator for activation request (login selection during activation)
/// </summary>
internal partial class ActivationRequestValidator : AbstractValidator<ActivationRequest>
{
    // Forbidden: control chars, HTML unsafe (<>), quotes ("'`), backslash, zero-width chars
    // Whitespace: not at start/end, not consecutive
    [GeneratedRegex(@"^(?!\s)(?!.*\s$)(?!.*\s{2})[^\p{Cc}<>""'`\\\u200B-\u200F\u2028-\u202F\uFEFF]{2,20}$")]
    private static partial Regex LoginValidationRegex();

    public ActivationRequestValidator(IRegistrationRepository registrationRepository)
    {
        RuleFor(r => r.Token)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(r => r.Login)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .Must(login => LoginValidationRegex().IsMatch(login))
            .WithMessage(ValidationError.Invalid)
            .MustAsync(registrationRepository.LoginFree).WithMessage(ValidationError.Taken);
    }
}
