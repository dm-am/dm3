using System;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Registration confirmation email sender for email-first flow.
/// Sends email verification link without username (username is chosen during activation).
/// </summary>
internal interface IRegistrationMailSender
{
    /// <summary>
    /// Sends the registration confirmation letter to newly registered user
    /// </summary>
    /// <param name="email">User email</param>
    /// <param name="token">Confirmation token</param>
    Task Send(string email, Guid token);
}
