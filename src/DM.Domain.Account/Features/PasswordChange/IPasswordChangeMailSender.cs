using System.Threading.Tasks;

namespace DM.Domain.Account.Features.PasswordChange;

/// <summary>
/// Password change mail sender
/// </summary>
internal interface IPasswordChangeMailSender
{
    /// <summary>
    /// Sends the password change notification to user
    /// </summary>
    /// <param name="email">User email</param>
    /// <param name="username">Username</param>
    /// <returns></returns>
    Task Send(string email, string username);
}
