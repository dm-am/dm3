using System.Threading.Tasks;

namespace DM.Domain.Account.Features.EmailChange;

/// <summary>
/// The two letters the previous address gets when the account is moved to another one
/// </summary>
/// <remarks>
/// One sender for both, because they are one story told twice to the same
/// mailbox. The first arrives while a new password still calls the move off, the
/// second after it went through.
/// </remarks>
public interface IEmailChangeNoticeMailSender
{
    /// <summary>
    /// Tell the current address that somebody asked to move the account
    /// </summary>
    /// <param name="currentEmail">Address the account has now</param>
    /// <param name="newEmail">Address the request names; the letter shows it masked</param>
    Task SendRequested(string currentEmail, string newEmail);

    /// <summary>
    /// Tell the previous address that the account has moved
    /// </summary>
    /// <param name="previousEmail">Address the account had</param>
    /// <param name="newEmail">Address it moved to; the letter shows it masked</param>
    Task SendCompleted(string previousEmail, string newEmail);
}
