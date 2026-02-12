using FluentValidation;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// Validator for password change requests
/// </summary>
/// <remarks>
/// Note: The "either Token or OldPassword must be provided" check is done in the service layer,
/// AFTER authentication check. This allows returning 401 for unauthenticated requests
/// instead of 400 validation error.
/// </remarks>
public class ChangePasswordValidator : AbstractValidator<ChangePassword>
{
    /// <summary>
    /// Constructor
    /// </summary>
    public ChangePasswordValidator()
    {
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .MaximumLength(128).WithMessage("Password must not exceed 128 characters");

        // When OldPassword is provided, validate its length
        When(x => !string.IsNullOrEmpty(x.OldPassword), () =>
        {
            RuleFor(x => x.OldPassword)
                .MaximumLength(128).WithMessage("Old password must not exceed 128 characters");
        });
    }
}
