using System;
using System.Collections.Generic;
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
/// Two readers exist and they want different things. Authentication goes on to
/// check the session against the store and refuses the request when it is gone.
/// The rate limiter only needs to tell one caller from another, and it runs
/// before authentication on purpose — being what keeps a flood away from the
/// database, it cannot afford anything else. Keeping the format, the property
/// names and the rule that an unreadable token is simply not a token in one
/// place is what stops the two from drifting: a renamed property would otherwise
/// leave the limiter silently unable to recognise anybody, and nothing about a
/// response, a log line or a metric would say so.
/// </remarks>
/// <param name="UserId">Account the token was issued to.</param>
/// <param name="SessionId">Session the token names.</param>
public sealed record SessionToken(Guid UserId, Guid SessionId)
{
    private const string UserIdKey = "userId";
    private const string SessionIdKey = "sessionId";

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
    /// <param name="token">Token as the caller sent it.</param>
    /// <returns>Contents of the token, or null when there are none to read.</returns>
    public static async Task<SessionToken?> Read(ISymmetricCryptoService cryptoService, string token)
    {
        try
        {
            var payload = await cryptoService.Decrypt(token);
            var contents = JsonSerializer.Deserialize<Dictionary<string, Guid>>(payload);
            return contents == null
                ? null
                : new SessionToken(contents[UserIdKey], contents[SessionIdKey]);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException
                                      or FormatException or CryptographicException)
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
        cryptoService.Encrypt(JsonSerializer.Serialize(new Dictionary<string, Guid>
        {
            [UserIdKey] = UserId,
            [SessionIdKey] = SessionId,
        }));
}
