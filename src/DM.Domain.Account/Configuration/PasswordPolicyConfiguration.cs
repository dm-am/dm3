namespace DM.Domain.Account.Configuration;

/// <summary>
/// Configuration for password policy requirements
/// </summary>
public class PasswordPolicyConfiguration
{
    /// <summary>
    /// Minimum password length.
    /// With Argon2id (19 MiB, 2 iterations), 8 characters is sufficient.
    /// </summary>
    public int MinimumLength { get; set; } = 8;

    /// <summary>
    /// Maximum password length.
    /// Prevents DoS via long password hashing. OWASP recommends max 128.
    /// Default: 128 characters
    /// </summary>
    public int MaximumLength { get; set; } = 128;

    /// <summary>
    /// Require at least one uppercase letter (A-Z).
    /// NIST SP 800-63B-4 (2024): SHALL NOT require composition rules.
    /// Default: false
    /// </summary>
    public bool RequireUppercase { get; set; } = false;

    /// <summary>
    /// Require at least one lowercase letter (a-z).
    /// NIST SP 800-63B-4 (2024): SHALL NOT require composition rules.
    /// Default: false
    /// </summary>
    public bool RequireLowercase { get; set; } = false;

    /// <summary>
    /// Require at least one digit (0-9).
    /// NIST SP 800-63B-4 (2024): SHALL NOT require composition rules.
    /// Default: false
    /// </summary>
    public bool RequireDigit { get; set; } = false;

    /// <summary>
    /// Require at least one special character (!@#$%^&amp;*...).
    /// NIST SP 800-63B-4 (2024): SHALL NOT require composition rules.
    /// Default: false
    /// </summary>
    public bool RequireSpecialCharacter { get; set; } = false;
}
