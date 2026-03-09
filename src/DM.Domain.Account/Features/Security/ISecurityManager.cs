namespace DM.Domain.Account.Features.Security;

/// <summary>
/// Password hashing using Argon2id (OWASP 2025)
/// </summary>
public interface ISecurityManager
{
    /// <summary>
    /// Generates hash and salt for password
    /// </summary>
    (string Hash, string Salt) GeneratePassword(string password);

    /// <summary>
    /// Compares password against stored hash
    /// </summary>
    bool ComparePasswords(string password, string salt, string hash);
}
