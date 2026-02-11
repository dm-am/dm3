using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.Account.Activation;

/// <summary>
/// Repository for email-first activation flow
/// </summary>
internal interface IActivationRepository
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
    /// <returns></returns>
    Task CompleteActivation(User user, Guid pendingId);

    /// <summary>
    /// Find user by email (for idempotent retry detection)
    /// </summary>
    /// <param name="email">User email</param>
    /// <param name="cancellationToken"></param>
    /// <returns>User if found</returns>
    Task<User?> FindUserByEmail(string email, CancellationToken cancellationToken = default);
}
