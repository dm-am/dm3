using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Account.Configuration;

/// <summary>
/// The password policy as validation rules, for every form that takes a new password
/// </summary>
internal static class PasswordPolicyRules
{
    /// <summary>
    /// The length always, the composition where the policy asks for it
    /// </summary>
    public static IRuleBuilderOptions<T, string> MeetsPasswordPolicy<T>(
        this IRuleBuilder<T, string> rule, PasswordPolicyConfiguration policy)
    {
        var options = rule
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MinimumLength(policy.MinimumLength).WithMessage(ValidationError.Short)
            .MaximumLength(policy.MaximumLength).WithMessage(ValidationError.Long);

        if (policy.RequireUppercase)
            options = options.Matches("[A-Z]").WithMessage(ValidationError.RequiresUppercase);
        if (policy.RequireLowercase)
            options = options.Matches("[a-z]").WithMessage(ValidationError.RequiresLowercase);
        if (policy.RequireDigit)
            options = options.Matches("[0-9]").WithMessage(ValidationError.RequiresDigit);
        if (policy.RequireSpecialCharacter)
            options = options.Matches("[^a-zA-Z0-9]").WithMessage(ValidationError.RequiresSpecialCharacter);

        return options;
    }
}
