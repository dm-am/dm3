using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Account.Recovery;

/// <summary>
/// Unified recovery service for password reset and activation resend
/// </summary>
public interface IRecoveryService
{
    /// <summary>
    /// Process recovery request for an email.
    /// - If email belongs to active user → sends password reset email
    /// - If email belongs to pending registration → resends activation email
    /// - If email not found → returns NotFound
    /// </summary>
    /// <param name="email">Email address</param>
    /// <returns>Recovery result indicating what action was taken</returns>
    Task<RecoveryResult> Recover(string email);

    /// <summary>
    /// Check if email is available for registration
    /// </summary>
    /// <param name="email">Email to check</param>
    /// <returns>Email availability status</returns>
    Task<EmailAvailabilityResult> CheckEmailAvailability(string email);
}
