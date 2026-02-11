using System.Collections.Generic;

namespace DM.Services.Authentication.Configuration;

/// <summary>
/// Configuration for AES-256-GCM authenticated encryption with key rotation support
/// </summary>
public class CryptoConfiguration
{
    /// <summary>
    /// Base64-encoded 32-byte (256-bit) AES key (current active key for encryption)
    /// Generate: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
    /// </summary>
    public string KeyBase64 { get; set; } = "";

    /// <summary>
    /// Current key version (incremented on rotation). Used as prefix in encrypted payloads.
    /// </summary>
    public int KeyVersion { get; set; } = 1;

    /// <summary>
    /// Previous keys for decryption only (key version → Base64-encoded key).
    /// Allows reading tokens encrypted with old keys during rotation.
    /// </summary>
    public Dictionary<int, string> PreviousKeys { get; set; } = new();

    /// <summary>
    /// Encryption algorithm (always AES-256-GCM)
    /// </summary>
    public string Algorithm { get; set; } = "AES-256-GCM";
}
