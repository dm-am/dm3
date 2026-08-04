using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.EmailChange;

/// <summary>
/// Email change confirmation sender
/// </summary>
internal interface IEmailChangeMailSender
{
    /// <summary>
    /// Sends the email change confirmation letter to the user
    /// </summary>
    /// <param name="email">User email</param>
    /// <param name="username">Username</param>
    /// <param name="token">Confirmation token</param>
    Task Send(string email, string username, Guid token);
}
