using System;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using FluentValidation;

namespace DM.Domain.Account.Features.EmailChange;

/// <inheritdoc />
internal class UserEmailChangeValidator : AbstractValidator<UserEmailChange>
{
    private const string FoundUserKey = nameof(FoundUserKey);

    /// <inheritdoc />
    public UserEmailChangeValidator(
        IEmailChangeRepository repository,
        ISecurityManager securityManager)
    {
        RuleFor(u => u.Username)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MustAsync(async (model, username, context, _) =>
            {
                var user = await repository.FindUser(username);
                if (user == null)
                {
                    return false;
                }

                context.RootContextData[FoundUserKey] = user;
                return true;
            }).WithMessage(ValidationError.Invalid);

        RuleFor(u => u.Password)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .Must((model, password, context) =>
                context.RootContextData.TryGetValue(FoundUserKey, out var userWrapper) &&
                userWrapper is AuthenticatedUser user &&
                securityManager.ComparePasswords(password, user.Salt, user.PasswordHash))
            .WithMessage(ValidationError.Invalid);

        RuleFor(u => u.Email)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .EmailAddress().WithMessage(ValidationError.Invalid)
            .Must((model, email, context) =>
            {
                if (!context.RootContextData.TryGetValue(FoundUserKey, out var userWrapper) ||
                    userWrapper is not AuthenticatedUser user) return true;
                return !email.Equals(user.Email, StringComparison.OrdinalIgnoreCase);
            })
            .WithMessage(ValidationError.Unchanged)
            .MustAsync(async (email, ct) => await repository.IsEmailFree(email, ct))
            .WithMessage(ValidationError.Taken);
    }
}
