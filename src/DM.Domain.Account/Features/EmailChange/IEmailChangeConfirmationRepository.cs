using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.EmailChange;

/// <summary>
/// Storage for email change confirmation
/// </summary>
public interface IEmailChangeConfirmationRepository
{
    /// <summary>
    /// Find valid email change token
    /// </summary>
    /// <param name="secret">Value the confirmation letter carried</param>
    /// <param name="createdSince">Minimum creation time</param>
    /// <returns>Token owner if found, null otherwise</returns>
    /// <remarks>
    /// Returns the owner rather than the row: the letter carries a secret and the
    /// row keeps only its hash, so the caller has no identifier to look anything
    /// up by afterwards.
    /// </remarks>
    Task<Guid?> FindEmailChangeTokenOwner(Guid secret, DateTimeOffset createdSince);

    /// <summary>
    /// Spend the link the secret belongs to
    /// </summary>
    /// <param name="secret">Value the confirmation letter carried</param>
    /// <remarks>
    /// By the same hash the lookup above matches on. Written as a search by row
    /// identifier, this spent nothing at all: the value in hand is the secret and
    /// the identifier is a different value, so the lookup found no row and the
    /// link stayed live.
    /// </remarks>
    Task MarkTokenUsed(Guid secret);
}
