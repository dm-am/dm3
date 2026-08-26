using DM.Domain.Core.Tokens;
using TokenEntity = DM.Infrastructure.Persistence.Entities.Account.Token;

namespace DM.Infrastructure.Persistence.Shared.Tokens;

/// <summary>
/// The row a <see cref="CreateToken" /> becomes.
/// </summary>
/// <remarks>
/// One text, because the four repositories that write a token had written it
/// four times and the copies had already drifted: one dropped the secret hash,
/// another the related entity. Neither producer of a CreateToken sets those two
/// members, so the drift changed nothing yet - it was simply waiting for the
/// first producer that did.
/// </remarks>
internal static class TokenRows
{
    public static TokenEntity From(CreateToken token) => new()
    {
        TokenId = token.TokenId,
        // The letter carries token.Secret; the row keeps only its hash.
        SecretHash = token.SecretHash,
        UserId = token.UserId,
        EntityId = token.EntityId,
        CreatedUtc = token.CreatedUtc,
        Type = token.Type,
        IsRemoved = false
    };
}
