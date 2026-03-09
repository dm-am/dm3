using System;
using System.Threading.Tasks;
using DM.Domain.Core.Tokens;

namespace DM.Domain.Account.Features.Recovery;

/// <summary>
/// Storage for password resetting
/// </summary>
public interface IPasswordResetRepository
{
    /// <summary>
    /// Invalidate old password reset tokens and create new one
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="token">New token data</param>
    Task ReplacePasswordResetToken(Guid userId, CreateToken token);
}
