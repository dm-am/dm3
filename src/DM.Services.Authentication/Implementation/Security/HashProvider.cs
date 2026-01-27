using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DM.Services.Authentication.Implementation.Security;

/// <inheritdoc />
internal class HashProvider : IHashProvider
{
    private readonly Lazy<SHA256> sha256 = new(SHA256.Create);

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
    public byte[] ComputeSha256(string plainText, string salt)
    {
        var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        var saltBytes = Convert.FromBase64String(salt);

        var buffer = plainTextBytes.Concat(saltBytes).ToArray();

        lock (sha256)
        {
            return sha256.Value.ComputeHash(buffer);
        }
    }

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
