using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.Account.Registration;

/// <summary>
/// Registration information storage for email-first registration flow
/// </summary>
internal interface IRegistrationRepository
{
    /// <summary>
    /// Tells if email is free for new registration (checks both Users and PendingRegistrations)
    /// </summary>
    /// <param name="email">Email</param>
    /// <param name="cancellationToken"></param>
    /// <returns>True if email can be used for registration</returns>
    Task<bool> EmailFreeForNewRegistration(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Tells if user with certain login is already registered (checks Users and LoginHistories)
    /// </summary>
    /// <param name="login">Login to check</param>
    /// <param name="cancellationToken"></param>
    /// <returns>True if login is available</returns>
    Task<bool> LoginFree(string login, CancellationToken cancellationToken);

    /// <summary>
    /// Checks if pending registration exists for this email
    /// </summary>
    /// <param name="email">Email</param>
    /// <param name="cancellationToken"></param>
    /// <returns>True if pending registration exists</returns>
    Task<bool> PendingExists(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Add new pending registration
    /// </summary>
    /// <param name="pending">Pending registration</param>
    /// <returns></returns>
    Task AddPending(PendingRegistration pending);

    /// <summary>
    /// Replace existing pending registration (re-registration with same email)
    /// Updates password and generates new token
    /// </summary>
    /// <param name="pending">New pending registration data</param>
    /// <returns></returns>
    Task ReplacePending(PendingRegistration pending);

    /// <summary>
    /// Find pending registration by email
    /// </summary>
    /// <param name="email">Email</param>
    /// <param name="cancellationToken"></param>
    /// <returns>Pending registration or null</returns>
    Task<PendingRegistration?> FindPendingByEmail(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Update pending registration (for resend activation)
    /// </summary>
    /// <param name="pending">Updated pending registration</param>
    /// <returns></returns>
    Task UpdatePending(PendingRegistration pending);

    /// <summary>
    /// Save password to history
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="passwordHash">Password hash</param>
    /// <param name="salt">Password salt</param>
    /// <param name="version">Hash version</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task SavePasswordToHistory(Guid userId, string passwordHash, string salt, int version, CancellationToken ct);
}
