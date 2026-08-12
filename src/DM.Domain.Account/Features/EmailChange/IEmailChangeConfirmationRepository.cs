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
    /// <param name="tokenId">Token identifier</param>
    /// <param name="createdSince">Minimum creation time</param>
    /// <returns>Token ID if found, null otherwise</returns>
    /// <remarks>Returns the owner of the token: the caller already holds the token id itself.</remarks>
    Task<Guid?> FindEmailChangeTokenOwner(Guid tokenId, DateTimeOffset createdSince);

    /// <summary>
    /// Mark the token as used
    /// </summary>
    Task MarkTokenUsed(Guid tokenId);
}
