using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Security;

/// <summary>
/// Service for symmetric cryptography
/// </summary>
public interface ISymmetricCryptoService
{
    /// <summary>
    /// Seals a value that stays in the server's own store.
    /// </summary>
    /// <remarks>
    /// Bound to no purpose, so it is not for a value a client carries: that one names
    /// its <see cref="SealPurpose"/>.
    /// </remarks>
    Task<string> Encrypt(string valueToEncrypt);

    Task<string> Decrypt(string valueToDecrypt);

    /// <summary>
    /// Seals a value for one purpose: it opens only where the same purpose is named.
    /// </summary>
    Task<string> Encrypt(string valueToEncrypt, SealPurpose purpose);

    /// <summary>
    /// Opens a value sealed for the purpose. One sealed for another purpose, or for
    /// none, is refused as a forged one is.
    /// </summary>
    Task<string> Decrypt(string valueToDecrypt, SealPurpose purpose);
}
