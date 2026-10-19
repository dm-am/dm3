using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Identity;
using DM.Domain.Account.Configuration;
using DM.Domain.Core.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.PasswordChange;

/// <inheritdoc />
internal class UserPasswordChangeValidator : AbstractValidator<UserPasswordChange>
{
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
                            identityProvider.Current.User.Salt, identityProvider.Current.User.PasswordHash))
                    .WithMessage(ValidationError.Invalid);
            });

        RuleFor(r => r.NewPassword).MeetsPasswordPolicy(passwordPolicy);
    }
}
