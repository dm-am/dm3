using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Account.PasswordChange.Confirmation;

/// <summary>
/// Password change notification email sender
/// </summary>
internal interface IPasswordChangeNotificationSender
{
    /// <summary>
    /// Sends the password change notification to user
    /// </summary>
    /// <param name="email">User email</param>
    /// <param name="login">User login</param>
    /// <returns></returns>
    Task Send(string email, string login);
}
