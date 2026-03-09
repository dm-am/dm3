using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Service for email-first user activation (username selection after email verification)
/// </summary>
public interface IActivationService
{
    /// <summary>
    /// Complete activation by selecting username
    /// </summary>
    /// <param name="request">Activation request with token and chosen username</param>
    /// <returns>Activated user identifier</returns>
    Task<Guid> Activate(ActivationRequest request);

    /// <summary>
    /// Get pending registration info by token (for UI pre-fill)
    /// </summary>
    /// <param name="tokenId">Token from activation link</param>
    /// <returns>Pending info (status, email) or null if not found</returns>
    Task<PendingInfoResult?> GetPendingInfo(Guid tokenId);

    /// <summary>
    /// Resend activation email for pending registration
    /// </summary>
    /// <param name="email">Email address</param>
    /// <returns>True if request was processed (always true to prevent enumeration)</returns>
    Task<bool> ResendActivation(string email);
}
