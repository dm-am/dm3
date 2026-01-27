namespace DM.Services.Authentication.Configuration;

/// <summary>
/// Configuration for symmetric encryption
/// </summary>
public class CryptoConfiguration
{
    /// <summary>
    /// Base64-encoded encryption key
    /// For TripleDES: 24 bytes (192 bits), produces ~32 char Base64
    /// For AES-256: 32 bytes (256 bits), produces ~44 char Base64
    /// </summary>
    public string KeyBase64 { get; set; }

    /// <summary>
    /// Base64-encoded initialization vector
    /// For TripleDES: 8 bytes (64 bits)
    /// For AES-GCM: 12 bytes (96 bits) - used as nonce
    /// </summary>
    public string IvBase64 { get; set; }

    /// <summary>
    /// Encryption algorithm: "TripleDES" or "AES-256-GCM"
    /// </summary>
    public string Algorithm { get; set; } = "TripleDES";
}
