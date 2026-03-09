namespace DM.Domain.Account.Features.Security;

/// <summary>
/// Provides password hash computation using Argon2id (OWASP 2025)
/// </summary>
internal interface IHashProvider
{
    /// <summary>
    /// Computes password hash using Argon2id
    /// </summary>
    byte[] ComputeHash(string plainText, string salt);
}
