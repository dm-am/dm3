namespace DM.Services.Authentication.Implementation.Security;

/// <summary>
/// Provides password hash computation
/// </summary>
internal interface IHashProvider
{
    /// <summary>
    /// Generates a byte sequence using SHA256 (legacy, version 1)
    /// </summary>
    byte[] ComputeSha256(string plainText, string salt);

    /// <summary>
    /// Generates a byte sequence using PBKDF2-SHA256 with 600,000 iterations (version 3)
    /// </summary>
    byte[] ComputePbkdf2(string plainText, string salt);

    /// <summary>
    /// Current hash version for new passwords
    /// </summary>
    int CurrentVersion { get; }
}
