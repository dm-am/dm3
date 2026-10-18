using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Deactivation;

/// <summary>
/// The removal itself, on a moderator's decision
/// </summary>
/// <remarks>
/// The removal is soft. The row stays, which is what keeps the address and the
/// name reserved and the content where it is; every session goes, because an
/// account that cannot be signed into must not stay signed in on a device
/// somewhere.
/// </remarks>
public interface IAccountRemover
{
    /// <summary>
    /// Remove the account
    /// </summary>
    /// <param name="userId">Whose account</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task Remove(Guid userId, CancellationToken cancellationToken = default);
}
