using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Account.PasswordReset;

/// <summary>
/// Service for password resetting
/// </summary>
public interface IPasswordResetService
{
    /// <summary>
    /// Reset user password. Always completes silently regardless of whether the account exists
    /// to prevent user enumeration attacks.
    /// </summary>
    /// <param name="passwordReset"></param>
    Task Reset(UserPasswordReset passwordReset);
}