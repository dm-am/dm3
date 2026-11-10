using DM.Domain.Core.Exceptions;
using FluentValidation;
using DM.Domain.Core.Users;

namespace DM.Domain.Account.Features.Registration;

internal class ActivationRequestValidator : AbstractValidator<ActivationRequest>
{
    public ActivationRequestValidator(IRegistrationRepository registrationRepository)
    {
        RuleFor(r => r.Token)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(r => r.Username)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .Must(UsernamePolicy.IsValid)
            .WithMessage(ValidationError.Invalid)
            .MustAsync(registrationRepository.UsernameFree).WithMessage(ValidationError.Taken);
    }
}
