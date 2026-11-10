using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Security;

namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// What the session cookie carries once it is opened: the account it was issued
/// to and the session it names.
/// </summary>
/// <remarks>
/// Read by authentication and by the rate limiter, which runs before it: the format
/// lives in one place so that the two cannot drift.
/// </remarks>
public sealed record SessionToken(Guid UserId, Guid SessionId)
{
    // The payload spelling of the tokens already issued: camelCase names, and both halves required,
    // so a payload missing one does not open to an empty id.
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };

    /// <summary>
    /// Opens a token. Null when it was not minted by this server, no longer
    /// decrypts under any key version, or does not carry both halves — forgery,
    /// corruption and a retired key are one answer here, and none of them names
    /// an account.
    /// </summary>
    /// <remarks>
    /// The cipher is AEAD, so a token that opens is one this server wrote and the
    /// pair inside it is the pair that was put there. Nothing further is
    /// established: the session may be gone, the account removed or banned. A
    /// caller that needs those answers has to ask the store for them.
    /// </remarks>
    /// <param name="cryptoService">Cipher the token was minted with.</param>
    /// <returns>Contents of the token, or null when there are none to read.</returns>
    public static async Task<SessionToken?> Read(ISymmetricCryptoService cryptoService, string token)
    {
        try
        {
            var payload = await cryptoService.Decrypt(token);
            return JsonSerializer.Deserialize<SessionToken>(payload, PayloadOptions);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or CryptographicException)
        {
            return null;
        }
    }

    /// <summary>
    /// Mints the token the caller will send back.
    /// </summary>
    /// <param name="cryptoService">Cipher to mint it with.</param>
    /// <returns>Encrypted token.</returns>
    public Task<string> Write(ISymmetricCryptoService cryptoService) =>
        cryptoService.Encrypt(JsonSerializer.Serialize(this, PayloadOptions));
}
