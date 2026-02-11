using System;
using System.Security.Cryptography;

namespace DM.Services.Authentication.Implementation.Security;

/// <inheritdoc />
internal class HashProvider : IHashProvider
{
    /// <summary>
    /// PBKDF2 iteration count (OWASP 2025 recommends 600,000 for SHA256)
    /// </summary>
    private const int Pbkdf2Iterations = 600_000;

    /// <summary>
    /// PBKDF2 output key length in bytes (256 bits)
    /// </summary>
    private const int Pbkdf2KeyLength = 32;

    /// <inheritdoc />
    public int CurrentVersion => 3;

    /// <inheritdoc />
    public byte[] ComputePbkdf2(string plainText, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);

        using var pbkdf2 = new Rfc2898DeriveBytes(
            plainText,
            saltBytes,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256);

        return pbkdf2.GetBytes(Pbkdf2KeyLength);
    }
}
