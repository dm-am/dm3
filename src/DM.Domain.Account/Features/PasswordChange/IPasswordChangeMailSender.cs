using System.Threading.Tasks;

namespace DM.Domain.Account.Features.PasswordChange;

internal interface IPasswordChangeMailSender
{
    /// <summary>
    /// Sends the password change notification to user
    /// </summary>
    /// <param name="email">User email</param>
    Task Send(string email);
}
