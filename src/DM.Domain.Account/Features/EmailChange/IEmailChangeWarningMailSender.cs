using System.Threading.Tasks;

namespace DM.Domain.Account.Features.EmailChange;

/// <summary>
/// Sends warning notification to OLD email about email change request
/// </summary>
public interface IEmailChangeWarningMailSender
{
    /// <summary>
    /// Send warning to old email address about pending email change
    /// </summary>
    /// <param name="oldEmail">Current (old) email address</param>
    /// <param name="username">Username</param>
    /// <param name="newEmail">New email address (masked)</param>
    Task SendAsync(string oldEmail, string username, string newEmail);
}
