using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Tokens;

namespace DM.Domain.Account.Features.TwoFactor;

/// <summary>
/// The two links the mailed removal path is made of, in the token store every
/// other confirmation link already lives in.
/// </summary>
public interface ITwoFactorRemovalTokenRepository
{
    /// <summary>
    /// Issue a link of one type, retiring any live link of the same type.
    /// </summary>
    /// <remarks>
    /// One live link per type per account: a second request must not leave the
    /// first letter working, because the reader of the older letter has no way
    /// to know it is older.
    /// </remarks>
    /// <param name="userId">Account</param>
    /// <param name="token">Token to issue</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ReplaceToken(Guid userId, CreateToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Spend a link, and only if it is live.
    /// </summary>
    /// <remarks>
    /// Conditional and one row: a link followed twice opens once. The value is
    /// looked up by its hash, because the row never held the value itself.
    /// </remarks>
    /// <param name="secret">Value from the link</param>
    /// <param name="type">Which of the two links this is</param>
    /// <param name="liveSince">Cutoff the lifetime puts on issue time, if any</param>
    /// <returns>Whose link it was, or null when there is nothing to spend</returns>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<Guid?> RedeemToken(
        Guid secret, TokenType type, DateTimeOffset? liveSince,
        CancellationToken cancellationToken = default);
}
