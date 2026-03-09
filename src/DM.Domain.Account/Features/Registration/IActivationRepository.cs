using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Repository for email-first activation flow
/// </summary>
public interface IActivationRepository
{
    /// <summary>
    /// Find pending registration by token ID
    /// </summary>
    /// <param name="tokenId">Token ID embedded in PendingRegistration</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Pending registration or null if not found</returns>
    Task<PendingRegistration?> FindPendingByToken(Guid tokenId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Complete activation: create User from PendingRegistration and delete pending
    /// </summary>
    /// <param name="user">User to create</param>
    /// <param name="pendingId">Pending registration ID to delete</param>
    Task CompleteActivation(CreateUser user, Guid pendingId);

    /// <summary>
    /// Find user by email (for idempotent retry detection)
    /// </summary>
    /// <param name="email">User email</param>
    /// <param name="cancellationToken"></param>
    /// <returns>User if found</returns>
    Task<AuthenticatedUser?> FindUserByEmail(string email, CancellationToken cancellationToken = default);
}
