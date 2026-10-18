using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Account.Features.Deactivation;

/// <summary>
/// Repository interface for account deactivation
/// </summary>
/// <remarks>
/// The three reads below are the product's only way to a removed row. Every
/// other repository reads users through the global filter, which hides removed
/// rows by design, so a removed account is not merely absent from the lists -
/// it cannot be found by name at all. Restoring one therefore starts here.
/// </remarks>
public interface IDeactivationRepository
{
    /// <summary>
    /// Gets user credentials for password verification during deactivation
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User credentials or null if user not found</returns>
    Task<UserCredentials?> GetUserCredentials(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a user as deactivated (soft delete)
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeactivateUser(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings a removed account back
    /// </summary>
    /// <remarks>
    /// The mirror of <see cref="DeactivateUser" />, and the only place in the
    /// product that clears the flag. The row never went anywhere, so the name,
    /// the address and everything written under the name come back with it.
    /// </remarks>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task Restore(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// One removed account by name
    /// </summary>
    /// <remarks>
    /// A live account is not an answer here: the name of a removed account stays
    /// reserved, so the two can never be the same row, and a read that returned
    /// the live one would let a restoration touch an account nobody removed.
    /// </remarks>
    /// <param name="username">Name the account had</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The account, or null when no removed account has that name</returns>
    Task<RemovedAccount?> GetRemovedByUsername(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many removed accounts the search matches
    /// </summary>
    /// <param name="search">Part of a name or of an address; empty counts them all</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<int> CountRemoved(string? search, CancellationToken cancellationToken = default);

    /// <summary>
    /// A page of removed accounts, by name
    /// </summary>
    /// <param name="search">Part of a name or of an address; empty lists them all</param>
    /// <param name="paging">Which page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IReadOnlyList<RemovedAccount>> GetRemoved(string? search, PagingData paging,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// User credentials for deactivation verification
/// </summary>
public class UserCredentials
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Password hash
    /// </summary>
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// Password salt
    /// </summary>
    public string Salt { get; set; } = null!;
}
