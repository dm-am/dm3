using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Tokens;
using Microsoft.EntityFrameworkCore;
using DbToken = DM.Infrastructure.Persistence.Entities.Account.Token;

namespace DM.Infrastructure.Persistence.Shared.Tokens;

/// <summary>
/// The row a <see cref="CreateToken" /> becomes, and the one way a token is spent.
/// </summary>
internal static class TokenRows
{
    public static DbToken From(CreateToken token) => new()
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

    /// <summary>Spends the matching tokens in one statement.</summary>
    public static Task<int> Retire(this IQueryable<DbToken> tokens, CancellationToken ct = default) =>
        tokens.ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRemoved, true), ct);
}
