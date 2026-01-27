namespace DM.Services.Authentication.Implementation.Security;

/// <summary>
/// Provides general operations for password security
/// </summary>
public interface ISecurityManager
{
    /// <summary>
    /// Generates pair of a hash and salt for given password using the current algorithm
    /// </summary>
    /// <param name="password">Plain password</param>
    /// <returns>Hash, salt, and version</returns>
    (string Hash, string Salt, int Version) GeneratePassword(string password);

    /// <summary>
    /// Compares a given password and pair of salt and hash using specified version
    /// </summary>
    /// <param name="password">Plain password</param>
    /// <param name="salt">Salt</param>
    /// <param name="hash">Hash</param>
    /// <param name="version">Hash algorithm version (1 = SHA256, 2 = PBKDF2)</param>
    /// <returns>If password is correct</returns>
    bool ComparePasswords(string password, string salt, string hash, int version);

    /// <summary>
    /// Checks if the hash needs to be upgraded to a newer algorithm
    /// </summary>
    /// <param name="version">Current hash version</param>
    /// <returns>True if rehash is needed</returns>
    bool NeedsRehash(int version);
}
