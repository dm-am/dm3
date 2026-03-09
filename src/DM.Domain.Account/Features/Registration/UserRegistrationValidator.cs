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
    /// <inheritdoc />
    public UserRegistrationValidator(
        IRegistrationRepository registrationRepository,
        IOptions<PasswordPolicyConfiguration> passwordPolicyOptions)
    {
        var passwordPolicy = passwordPolicyOptions.Value;

        // Email must be unique across both Users and PendingRegistrations
        RuleFor(r => r.Email)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(100).WithMessage(ValidationError.Long)
            .EmailAddress().WithMessage(ValidationError.Invalid)
            .MustAsync(registrationRepository.EmailFreeForNewRegistration).WithMessage(ValidationError.Taken);

        RuleFor(r => r.Password)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MinimumLength(passwordPolicy.MinimumLength).WithMessage(ValidationError.Short)
            .MaximumLength(passwordPolicy.MaximumLength).WithMessage(ValidationError.Long);

        RuleFor(r => r.AcceptedRules)
            .Equal(true).WithMessage("Необходимо принять правила сайта");

        // Apply conditional password complexity rules based on configuration
        if (passwordPolicy.RequireUppercase)
        {
            RuleFor(r => r.Password)
                .Matches("[A-Z]").WithMessage(ValidationError.RequiresUppercase);
        }

        if (passwordPolicy.RequireLowercase)
        {
            RuleFor(r => r.Password)
                .Matches("[a-z]").WithMessage(ValidationError.RequiresLowercase);
        }

        if (passwordPolicy.RequireDigit)
        {
            RuleFor(r => r.Password)
                .Matches("[0-9]").WithMessage(ValidationError.RequiresDigit);
        }

        if (passwordPolicy.RequireSpecialCharacter)
        {
            RuleFor(r => r.Password)
                .Matches("[^a-zA-Z0-9]").WithMessage(ValidationError.RequiresSpecialCharacter);
        }
    }
}