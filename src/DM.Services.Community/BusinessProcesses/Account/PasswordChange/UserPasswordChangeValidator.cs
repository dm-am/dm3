using System;
using DM.Services.Authentication.Implementation.Security;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Community.Configuration;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace DM.Services.Community.BusinessProcesses.Account.PasswordChange;

/// <inheritdoc />
internal class UserPasswordChangeValidator : AbstractValidator<UserPasswordChange>
{
    /// <inheritdoc />
    public UserPasswordChangeValidator(
        IPasswordChangeRepository passwordChangeRepository,
        IDateTimeProvider dateTimeProvider,
        IIdentityProvider identityProvider,
        ISecurityManager securityManager,
        IOptions<PasswordPolicyConfiguration> passwordPolicyOptions,
        IOptions<TokenConfiguration> tokenOptions)
    {
        var passwordPolicy = passwordPolicyOptions.Value;
        var tokenConfig = tokenOptions.Value;

        When(c => c.Token.HasValue, () =>
                RuleFor(c => c.Token!.Value)
                    .NotEmpty().WithMessage(ValidationError.Empty)
                    .MustAsync(async (token, _) =>
                    {
                        var tokenValid = await passwordChangeRepository.TokenValid(
                            token, dateTimeProvider.Now - TimeSpan.FromHours(tokenConfig.PasswordResetTokenLifetimeHours));
                        return tokenValid;
                    })
                    .WithMessage(ValidationError.Invalid))
            .Otherwise(() =>
            {
                RuleFor(c => c.OldPassword)
                    .NotEmpty().WithMessage(ValidationError.Empty)
                    .Must((model, password, context) =>
                        identityProvider.Current.User.IsAuthenticated &&
                        securityManager.ComparePasswords(password,
                            identityProvider.Current.User.Salt, identityProvider.Current.User.PasswordHash, identityProvider.Current.User.PasswordHashVersion))
                    .WithMessage(ValidationError.Invalid);
            });

        // Apply same password policy as registration
        RuleFor(r => r.NewPassword)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MinimumLength(passwordPolicy.MinimumLength).WithMessage(ValidationError.Short)
            .MaximumLength(passwordPolicy.MaximumLength).WithMessage(ValidationError.Long);

        if (passwordPolicy.RequireUppercase)
        {
            RuleFor(r => r.NewPassword)
                .Matches("[A-Z]").WithMessage(ValidationError.RequiresUppercase);
        }

        if (passwordPolicy.RequireLowercase)
        {
            RuleFor(r => r.NewPassword)
                .Matches("[a-z]").WithMessage(ValidationError.RequiresLowercase);
        }

        if (passwordPolicy.RequireDigit)
        {
            RuleFor(r => r.NewPassword)
                .Matches("[0-9]").WithMessage(ValidationError.RequiresDigit);
        }

        if (passwordPolicy.RequireSpecialCharacter)
        {
            RuleFor(r => r.NewPassword)
                .Matches("[^a-zA-Z0-9]").WithMessage(ValidationError.RequiresSpecialCharacter);
        }
    }
}