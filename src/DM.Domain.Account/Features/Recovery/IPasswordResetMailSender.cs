using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Recovery;

/// <summary>
/// Password resetting mail sender
/// </summary>
internal interface IPasswordResetMailSender
{
    /// <summary>
    /// Sends the password reset confirmation letter to registered user
    /// </summary>
    /// <param name="email">User email</param>
    /// <param name="username">Username</param>
    /// <param name="token">Confirmation token</param>
    /// <returns></returns>
    Task Send(string email, string username, Guid token);
}