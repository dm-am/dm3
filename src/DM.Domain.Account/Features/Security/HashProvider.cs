using System;
using System.Text;
using Konscious.Security.Cryptography;

namespace DM.Domain.Account.Features.Security;

/// <inheritdoc />
internal class HashProvider : IHashProvider
{
    /// <summary>
    /// Output key length in bytes (256 bits)
    /// </summary>
    private const int KeyLength = 32;

    /// <summary>
    /// Argon2id memory size in KB (19 MiB = OWASP 2025 primary recommendation)
    /// </summary>
    private const int Argon2MemorySize = 19 * 1024; // 19456 KB = 19 MiB

    /// <summary>
    /// Argon2id iterations (time cost)
    /// </summary>
    private const int Argon2Iterations = 2;

    /// <summary>
    /// Argon2id parallelism (degree of parallelism)
    /// </summary>
    private const int Argon2Parallelism = 1;

    /// <inheritdoc />
    public byte[] ComputeHash(string plainText, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var passwordBytes = Encoding.UTF8.GetBytes(plainText);

        using var argon2 = new Argon2id(passwordBytes);
        argon2.Salt = saltBytes;
        argon2.MemorySize = Argon2MemorySize;
        argon2.Iterations = Argon2Iterations;
        argon2.DegreeOfParallelism = Argon2Parallelism;

        return argon2.GetBytes(KeyLength);
    }
}
