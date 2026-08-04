using System;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Account.Features.PasswordChange;

/// <summary>
/// Service for user password changing
/// </summary>
public interface IPasswordChangeService
{
    /// <summary>
    /// Check if password reset token is valid
    /// </summary>
    /// <param name="tokenId">Token identifier</param>
    /// <returns>Token status: ready, expired, or null if not found</returns>
    Task<PasswordResetTokenInfo?> GetTokenInfo(Guid tokenId);

    /// <summary>
    /// Change user password
    /// </summary>
    /// <param name="passwordChange">Password change data</param>
    /// <returns>Updated user</returns>
    Task<GeneralUser> Change(UserPasswordChange passwordChange);
}

/// <summary>
/// Password reset token information
/// </summary>
public record PasswordResetTokenInfo(string Status)
{
    /// <summary>
    /// Token is valid and ready to use
    /// </summary>
    public static PasswordResetTokenInfo Ready() => new("ready");

    /// <summary>
    /// Token has expired
    /// </summary>
    public static PasswordResetTokenInfo Expired() => new("expired");
}
