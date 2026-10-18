using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Deactivation;

/// <summary>
/// The letter a restored account earns
/// </summary>
/// <remarks>
/// Nobody was waiting for an answer here, because nobody filed anything: a
/// moderator brought the account back and this letter is how its owner learns
/// of it, at the address the account kept while it was gone.
/// </remarks>
public interface IAccountRestorationMailSender
{
    /// <summary>
    /// The account answers again
    /// </summary>
    /// <param name="email">Address of the account</param>
    Task SendRestored(string email);
}
