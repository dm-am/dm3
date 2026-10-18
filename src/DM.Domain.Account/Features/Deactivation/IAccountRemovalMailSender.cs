using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Deactivation;

/// <summary>
/// The letter a removed account earns
/// </summary>
/// <remarks>
/// The account keeps its address, so the letter reaches the same mailbox it
/// always did. It is the only notice the owner gets: the decision was a
/// moderator's, and the way back runs through support.
/// </remarks>
public interface IAccountRemovalMailSender
{
    /// <summary>
    /// The account is gone
    /// </summary>
    /// <param name="email">Address the account had</param>
    Task SendCompleted(string email);
}
