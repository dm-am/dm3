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

    /// <inheritdoc />
    public AesGcmSymmetricCryptoService(IOptions<CryptoConfiguration> cryptoOptions)
    {
        var config = cryptoOptions.Value;

        if (string.IsNullOrEmpty(config?.KeyBase64))
        {
            throw new InvalidOperationException(
                "CryptoConfiguration.KeyBase64 must be configured. " +
                "Generate a secure 32-byte key: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))");
        }

        _currentKey = Convert.FromBase64String(config.KeyBase64);
        _currentVersion = config.KeyVersion;

        if (_currentKey.Length != 32)
        {
            throw new CryptographicException($"AES-256 key must be 32 bytes (256 bits), got {_currentKey.Length} bytes");
        }

        _allKeys[_currentVersion] = _currentKey;

        // Load previous keys for decryption
        if (config.PreviousKeys != null)
        {
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
    }

    /// <inheritdoc />
    public Task<string> Encrypt(string valueToEncrypt)
    {
        var plaintext = Encoding.UTF8.GetBytes(valueToEncrypt);

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(_currentKey, TagSize);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        // Format: [version(1)][nonce(12)][tag(16)][ciphertext(N)]
        var result = new byte[VersionSize + NonceSize + TagSize + ciphertext.Length];
        result[0] = (byte)_currentVersion;
        Buffer.BlockCopy(nonce, 0, result, VersionSize, NonceSize);
        Buffer.BlockCopy(tag, 0, result, VersionSize + NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, result, VersionSize + NonceSize + TagSize, ciphertext.Length);

        return Task.FromResult(Convert.ToBase64String(result));
    }

    /// <inheritdoc />
    public Task<string> Decrypt(string valueToDecrypt)
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

        return Task.FromResult(DecryptWithKey(combined, VersionSize, key));
    }

    private static string DecryptWithKey(byte[] combined, int offset, byte[] key)
    {
        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var ciphertext = new byte[combined.Length - offset - NonceSize - TagSize];

        Buffer.BlockCopy(combined, offset, nonce, 0, NonceSize);
        Buffer.BlockCopy(combined, offset + NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(combined, offset + NonceSize + TagSize, ciphertext, 0, ciphertext.Length);

        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
