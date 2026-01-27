using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DM.Services.Authentication.Configuration;
using Microsoft.Extensions.Options;

namespace DM.Services.Authentication.Implementation.Security;

/// <inheritdoc />
internal class TripleDesSymmetricCryptoService : ISymmetricCryptoService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;
    private readonly Lazy<TripleDES> _tripleDesService = new(TripleDES.Create);

    // Fallback keys for backward compatibility during migration
    // WARNING: These should be removed after all deployments are updated
    private const string FallbackKey = "QkEeenXpHqgP6tOWwpUetAFvUUZiMb4f";
    private const string FallbackIv = "dtEzMsz2ogg=";

    /// <inheritdoc />
    public TripleDesSymmetricCryptoService(IOptions<CryptoConfiguration> cryptoOptions)
    {
        var config = cryptoOptions.Value;

        // Use configured keys if available, otherwise fall back to hardcoded (for migration)
        var keyBase64 = !string.IsNullOrEmpty(config?.KeyBase64) ? config.KeyBase64 : FallbackKey;
        var ivBase64 = !string.IsNullOrEmpty(config?.IvBase64) ? config.IvBase64 : FallbackIv;

        _key = Convert.FromBase64String(keyBase64);
        _iv = Convert.FromBase64String(ivBase64);

        // Validate key and IV lengths for TripleDES
        if (_key.Length != 24)
        {
            throw new CryptographicException($"TripleDES key must be 24 bytes (192 bits), got {_key.Length} bytes");
        }

        if (_iv.Length != 8)
        {
            throw new CryptographicException($"TripleDES IV must be 8 bytes (64 bits), got {_iv.Length} bytes");
        }
    }

    /// <inheritdoc />
    public async Task<string> Encrypt(string valueToEncrypt)
    {
        using var encryptedStream = new MemoryStream();
        await using (var stream = new CryptoStream(encryptedStream,
                         _tripleDesService.Value.CreateEncryptor(_key, _iv),
                         CryptoStreamMode.Write))
        {
            var data = Encoding.UTF8.GetBytes(valueToEncrypt);
            await stream.WriteAsync(data);
        }

        return Convert.ToBase64String(encryptedStream.ToArray());
    }

    /// <inheritdoc />
    public async Task<string> Decrypt(string valueToDecrypt)
    {
        using var decryptedStream = new MemoryStream();
        await using (var stream = new CryptoStream(decryptedStream,
                         _tripleDesService.Value.CreateDecryptor(_key, _iv),
                         CryptoStreamMode.Write))
        {
            var encryptedData = Convert.FromBase64String(valueToDecrypt);
            await stream.WriteAsync(encryptedData);
        }

        return Encoding.UTF8.GetString(decryptedStream.ToArray());
    }
}
