using System;
using System.Threading.Tasks;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Account.Availability;

namespace DM.Web.API.Features.Account.Recovery;

/// <summary>
/// API service for account recovery operations
/// </summary>
public interface IRecoveryApiService
{
    /// <summary>
    /// Process account recovery request
    /// </summary>
    /// <param name="request">Recovery request with email</param>
    /// <returns>Recovery result</returns>
    Task<RecoveryResponse> Recover(RecoveryRequest request);

    /// <summary>
    /// Check if email is available for registration
    /// </summary>
    /// <param name="email">Email to check</param>
    /// <returns>Availability status</returns>
    Task<EmailAvailabilityResponse> CheckEmailAvailability(string email);

    /// <summary>
    /// Get password reset token info
    /// </summary>
    /// <param name="token">Password reset token</param>
    /// <returns>Token info or null if not found</returns>
    Task<PasswordResetTokenInfo?> GetTokenInfo(Guid token);

    /// <summary>
    /// Complete password reset using token
    /// </summary>
    /// <param name="token">Password reset token</param>
    /// <param name="request">New password</param>
    /// <returns>Updated user</returns>
    Task<User> ResetPassword(Guid token, PasswordResetCompletion request);
}
