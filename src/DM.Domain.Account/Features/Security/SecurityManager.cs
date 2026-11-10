using System;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace DM.Domain.Account.Features.Security;

/// <inheritdoc />
internal class SecurityManager : ISecurityManager
{
    // Output key length in bytes (256 bits)
    private const int KeyLength = 32;

    // Argon2id memory size in KB (19 MiB = OWASP 2025 primary recommendation)
    private const int Argon2MemorySize = 19 * 1024;

    private const int Argon2Iterations = 2;

    private const int Argon2Parallelism = 1;

    /// <inheritdoc />
    public (string Hash, string Salt) GeneratePassword(string password)
    {
        var salt = SaltFactory.Create(100);
        var hash = ComputeHash(password, salt);
        return (Convert.ToBase64String(hash), salt);
    }

    /// <inheritdoc />
    public bool ComparePasswords(string password, string salt, string hash)
    {
        var computedHash = ComputeHash(password, salt);
        var storedHash = Convert.FromBase64String(hash);

        // Constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
    }

    private static byte[] ComputeHash(string plainText, string salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(plainText));
        argon2.Salt = Convert.FromBase64String(salt);
        argon2.MemorySize = Argon2MemorySize;
        argon2.Iterations = Argon2Iterations;
        argon2.DegreeOfParallelism = Argon2Parallelism;

        return argon2.GetBytes(KeyLength);
    }
}
