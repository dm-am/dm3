using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Security;

/// <summary>
/// Service for symmetric cryptography
/// </summary>
public interface ISymmetricCryptoService
{
    Task<string> Encrypt(string valueToEncrypt);

    Task<string> Decrypt(string valueToDecrypt);
}
