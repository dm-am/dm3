using DM.Domain.Account.Configuration;
using DM.Domain.Core.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Validator for user registration DTO model (email-first flow).
/// Username validation happens during activation, not registration.
/// </summary>
internal class UserRegistrationValidator : AbstractValidator<UserRegistration>
{
    public UserRegistrationValidator(
        IRegistrationRepository registrationRepository,
        IOptions<PasswordPolicyConfiguration> passwordPolicyOptions)
    {
        var passwordPolicy = passwordPolicyOptions.Value;

        // Email must be unique across both Users and PendingRegistrations
        RuleFor(r => r.Email)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(100).WithMessage(ValidationError.Long)
            .EmailAddress().WithMessage(RefusalMessage.EmailInvalid)
            .MustAsync(registrationRepository.EmailFreeForNewRegistration).WithMessage(ValidationError.Taken);

        RuleFor(r => r.Password).MeetsPasswordPolicy(passwordPolicy);

        RuleFor(r => r.AcceptedRules)
            .Equal(true).WithMessage("Необходимо принять правила сайта");
    }
}
