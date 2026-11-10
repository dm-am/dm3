using DM.Domain.Account.Configuration;

namespace DM.Domain.Account.Tests.Dsl;

// Password policies for the validators of every form that takes a new password.
internal static class PasswordPolicies
{
    public const int MaximumLength = 128;

    // Eight to <see cref="MaximumLength"/> characters, with the composition asked for.
    public static PasswordPolicyConfiguration Requiring(
        bool uppercase = false, bool lowercase = false, bool digit = false, bool specialCharacter = false) => new()
        {
            MinimumLength = 8,
            MaximumLength = MaximumLength,
            RequireUppercase = uppercase,
            RequireLowercase = lowercase,
            RequireDigit = digit,
            RequireSpecialCharacter = specialCharacter
        };
}
