using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Registration information storage for email-first registration flow
/// </summary>
public interface IRegistrationRepository
{
    /// <summary>
    /// Tells if email is free for new registration (checks both Users and PendingRegistrations)
    /// </summary>
    Task<bool> EmailFreeForNewRegistration(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Tells if user with certain username is already registered (checks Users and UsernameHistories)
    /// </summary>
    Task<bool> UsernameFree(string username, CancellationToken cancellationToken);

    /// <summary>
    /// Record a pending registration, replacing whatever the address already has
    /// </summary>
    /// <remarks>
    /// One write, not a check and an insert, so two submissions cannot race. The row
    /// keeps its original start date, which the sweep of abandoned addresses counts from.
    /// </remarks>
    Task AddPending(PendingRegistration pending);

    Task<PendingRegistration?> FindPendingByEmail(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Update pending registration (for resend activation)
    /// </summary>
    Task UpdatePending(PendingRegistration pending);

    /// <summary>
    /// Delete pending registrations started before <paramref name="startedBefore" />.
    /// </summary>
    /// <remarks>
    /// The address a pending registration holds is not free for anybody else while
    /// the row is there, so this is what returns it — the window it is derived
    /// from is <see cref="Configuration.AccountRetentionPolicy.PendingRegistrationLifetime" />.
    /// </remarks>
    /// <param name="startedBefore">Cutoff the caller derived from the retention policy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of rows removed.</returns>
    Task<int> DeletePendingStartedBefore(DateTimeOffset startedBefore, CancellationToken cancellationToken = default);
}
