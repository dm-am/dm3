using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Recovery;

internal interface IPasswordResetMailSender
{
    /// <summary>
    /// Sends the password reset confirmation letter to registered user
    /// </summary>
    /// <param name="email">User email</param>
    /// <param name="token">Confirmation token</param>
    Task Send(string email, Guid token);
}
