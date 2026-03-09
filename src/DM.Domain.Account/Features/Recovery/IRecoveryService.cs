using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Recovery;

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
}
