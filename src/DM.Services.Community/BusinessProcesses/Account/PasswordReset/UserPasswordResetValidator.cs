using DM.Services.Core.Exceptions;
using FluentValidation;

namespace DM.Services.Community.BusinessProcesses.Account.PasswordReset;

/// <inheritdoc />
internal class UserPasswordResetValidator : AbstractValidator<UserPasswordReset>
{
    /// <inheritdoc />
    public UserPasswordResetValidator()
    {
        RuleFor(r => r.Login)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(r => r.Email)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .EmailAddress().WithMessage(ValidationError.Invalid);
    }
}