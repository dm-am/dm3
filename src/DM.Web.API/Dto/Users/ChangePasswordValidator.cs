using FluentValidation;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// Validator for password change requests
/// </summary>
public class ChangePasswordValidator : AbstractValidator<ChangePassword>
{
    /// <summary>
    /// Constructor
    /// </summary>
    public ChangePasswordValidator()
    {
        RuleFor(x => x.Login)
            .NotEmpty().WithMessage("Login is required")
            .MinimumLength(2).WithMessage("Login must be at least 2 characters")
            .MaximumLength(20).WithMessage("Login must not exceed 20 characters");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .MaximumLength(128).WithMessage("Password must not exceed 128 characters");

        // At least one of Token or OldPassword must be provided
        RuleFor(x => x)
            .Must(x => x.Token.HasValue || !string.IsNullOrEmpty(x.OldPassword))
            .WithMessage("Either a password reset token or the current password must be provided");

        // When OldPassword is provided, validate its length
        When(x => !string.IsNullOrEmpty(x.OldPassword), () =>
        {
            RuleFor(x => x.OldPassword)
                .MaximumLength(128).WithMessage("Old password must not exceed 128 characters");
        });
    }
}
