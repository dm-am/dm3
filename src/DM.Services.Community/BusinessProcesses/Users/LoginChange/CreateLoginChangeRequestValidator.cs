using DM.Services.Community.BusinessProcesses.Account.Registration;
using DM.Services.Community.BusinessProcesses.Users.LoginHistory;
using DM.Services.Core.Exceptions;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Users.LoginChange;

/// <inheritdoc />
internal class CreateLoginChangeRequestValidator : AbstractValidator<CreateLoginChangeRequest>
{
    public CreateLoginChangeRequestValidator(
        IRegistrationRepository registrationRepository,
        ILoginHistoryRepository loginHistoryRepository)
    {
        // Forbidden: control chars, HTML unsafe (<>), quotes ("'`), backslash, zero-width chars
        // Whitespace: not at start/end, not consecutive
        RuleFor(r => r.RequestedLogin)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .Matches(@"^(?!\s)(?!.*\s$)(?!.*\s{2})[^\p{Cc}<>""'`\\\u200B-\u200F\u2028-\u202F\uFEFF]{2,20}$")
            .WithMessage(ValidationError.Invalid)
            .MustAsync(async (login, ct) => await registrationRepository.LoginFree(login, ct))
            .WithMessage(ValidationError.Taken)
            .MustAsync(async (login, ct) => !await loginHistoryRepository.IsLoginReserved(login, ct))
            .WithMessage(ValidationError.Taken);

        RuleFor(r => r.Reason)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(500).WithMessage(ValidationError.Long);
    }
}
