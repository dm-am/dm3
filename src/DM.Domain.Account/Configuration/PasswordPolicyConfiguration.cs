namespace DM.Domain.Account.Configuration;

/// <remarks>
/// The composition rules are off: NIST SP 800-63B-4 (2024) SHALL NOT require them.
/// </remarks>
public class PasswordPolicyConfiguration
{
    /// <summary>
    /// Minimum password length: a shorter password falls to guessing.
    /// With Argon2id (19 MiB, 2 iterations), 8 characters is sufficient.
    /// </summary>
    public int MinimumLength { get; set; } = 8;

    /// <summary>
    /// Maximum password length.
    /// Prevents DoS via long password hashing. OWASP recommends max 128.
    /// </summary>
    public int MaximumLength { get; set; } = 128;

    /// <summary>
    /// Require at least one uppercase letter (A-Z).
    /// </summary>
    public bool RequireUppercase { get; set; }

    /// <summary>
    /// Require at least one lowercase letter (a-z).
    /// </summary>
    public bool RequireLowercase { get; set; }

    /// <summary>
    /// Require at least one digit (0-9).
    /// </summary>
    public bool RequireDigit { get; set; }

    /// <summary>
    /// Require at least one special character (!@#$%^&amp;*...).
    /// </summary>
    public bool RequireSpecialCharacter { get; set; }
}
