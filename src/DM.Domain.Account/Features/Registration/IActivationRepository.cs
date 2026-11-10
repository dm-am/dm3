using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Repository for email-first activation flow
/// </summary>
public interface IActivationRepository
{
    Task<PendingRegistration?> FindPendingByToken(Guid tokenId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Complete activation: create User from PendingRegistration and delete pending
    /// </summary>
    /// <remarks>
    /// Throws <see cref="DuplicateEntityException" /> when the name or the address
    /// stopped being free between the check and this write. Which of the two it
    /// was is left to the caller to establish by reading, rather than taken from
    /// the name of the constraint: the same collision is an idempotent second
    /// click for one person and a name somebody else has taken for another.
    /// </remarks>
    /// <param name="user">User to create</param>
    /// <param name="pendingId">Pending registration ID to delete</param>
    Task CompleteActivation(CreateUser user, Guid pendingId);

    /// <summary>
    /// Find user by email (for idempotent retry detection)
    /// </summary>
    Task<AuthenticatedUser?> FindUserByEmail(string email, CancellationToken cancellationToken = default);
}
