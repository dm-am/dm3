using System;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.Account.PasswordReset;

/// <summary>
/// Storage for password resetting
/// </summary>
internal interface IPasswordResetRepository
{
    /// <summary>
    /// Invalidate old password reset tokens and create new one
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="token">New token</param>
    /// <returns></returns>
    Task ReplacePasswordResetToken(Guid userId, Token token);
}