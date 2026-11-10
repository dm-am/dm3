using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.Security;

/// <summary>
/// AES-256-GCM authenticated encryption service (AEAD) with key rotation support.
/// Encrypted payload format: [1 byte version][12 bytes nonce][16 bytes tag][N bytes ciphertext]
/// </summary>
internal class AesGcmSymmetricCryptoService : ISymmetricCryptoService
{
    private const int NonceSize = 12; // 96 bits - standard for GCM
    private const int TagSize = 16;   // 128 bits - authentication tag
    private const int VersionSize = 1; // 1 byte for key version prefix

    private readonly byte[] _currentKey;
    private readonly int _currentVersion;
    private readonly Dictionary<int, byte[]> _allKeys = new();

    public AesGcmSymmetricCryptoService(IOptions<CryptoConfiguration> cryptoOptions)
    {
        // The current key is checked once, at startup (AccountConfigurationExtensions);
        // the previous ones are not, and are checked here.
        var config = cryptoOptions.Value;
        _currentKey = Convert.FromBase64String(config.KeyBase64);
        _currentVersion = config.KeyVersion;
        _allKeys[_currentVersion] = _currentKey;

        foreach (var (version, keyBase64) in config.PreviousKeys)
        {
            var key = Convert.FromBase64String(keyBase64);
            if (key.Length != 32)
            {
                throw new CryptographicException($"Previous key version {version} must be 32 bytes, got {key.Length} bytes");
            }
            _allKeys[version] = key;
        }
    }

    /// <inheritdoc />
    public Task<string> Encrypt(string valueToEncrypt) =>
        Task.FromResult(Seal(valueToEncrypt, default));

    /// <inheritdoc />
    public Task<string> Encrypt(string valueToEncrypt, SealPurpose purpose) =>
        Task.FromResult(Seal(valueToEncrypt, AssociatedData(purpose)));

    /// <inheritdoc />
    public Task<string> Decrypt(string valueToDecrypt) =>
        Task.FromResult(Open(valueToDecrypt, default));

    /// <inheritdoc />
    public Task<string> Decrypt(string valueToDecrypt, SealPurpose purpose) =>
        Task.FromResult(Open(valueToDecrypt, AssociatedData(purpose)));

    // Authenticated, not stored: the envelope is one for every purpose, and the tag
    // holds only under the purpose the value was sealed for.
    private static ReadOnlySpan<byte> AssociatedData(SealPurpose purpose) => purpose switch
    {
        SealPurpose.SessionToken => "session-token"u8,
        SealPurpose.TwoFactorChallenge => "two-factor-challenge"u8,
        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null)
    };

    private string Seal(string valueToEncrypt, ReadOnlySpan<byte> associatedData)
    {
        var plaintext = Encoding.UTF8.GetBytes(valueToEncrypt);

        // Format: [version(1)][nonce(12)][tag(16)][ciphertext(N)], written in place
        var result = new byte[VersionSize + NonceSize + TagSize + plaintext.Length];
        result[0] = (byte)_currentVersion;
        var nonce = result.AsSpan(VersionSize, NonceSize);
        RandomNumberGenerator.Fill(nonce);

        using var aesGcm = new AesGcm(_currentKey, TagSize);
        aesGcm.Encrypt(nonce, plaintext, result.AsSpan(VersionSize + NonceSize + TagSize),
            result.AsSpan(VersionSize + NonceSize, TagSize), associatedData);

        return Convert.ToBase64String(result);
    }

    private string Open(string valueToDecrypt, ReadOnlySpan<byte> associatedData)
    {
        var combined = Convert.FromBase64String(valueToDecrypt);

        if (combined.Length < VersionSize + NonceSize + TagSize)
        {
            throw new CryptographicException("Invalid encrypted data: too short");
        }

        var version = (int)combined[0];
        if (!_allKeys.TryGetValue(version, out var key))
        {
            throw new CryptographicException($"Unknown encryption key version: {version}");
        }

        return DecryptWithKey(combined, key, associatedData);
    }

    private static string DecryptWithKey(byte[] combined, byte[] key, ReadOnlySpan<byte> associatedData)
    {
        var payload = combined.AsSpan(VersionSize);
        var nonce = payload[..NonceSize];
        var tag = payload.Slice(NonceSize, TagSize);
        var ciphertext = payload[(NonceSize + TagSize)..];

        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);

        return Encoding.UTF8.GetString(plaintext);
    }
}
