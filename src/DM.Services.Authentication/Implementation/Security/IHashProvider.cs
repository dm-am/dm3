namespace DM.Services.Authentication.Implementation.Security;

/// <summary>
/// Provides password hash computation using PBKDF2-SHA256
/// </summary>
internal interface IHashProvider
{
    /// <summary>
    /// Generates a byte sequence using PBKDF2-SHA256 with 600,000 iterations (OWASP 2025)
    /// </summary>
    byte[] ComputePbkdf2(string plainText, string salt);

    /// <summary>
    /// Current hash version for new passwords (always 3 for PBKDF2)
    /// </summary>
    int CurrentVersion { get; }
}
