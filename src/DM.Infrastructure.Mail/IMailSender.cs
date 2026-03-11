using System.Threading.Tasks;

namespace DM.Infrastructure.Mail;

/// <summary>
/// Sends the defined email.
/// </summary>
/// <remarks>
/// DEPRECATED: Use <see cref="DM.Domain.Core.Mail.IMailSender"/> from Domain.Core.Mail instead.
/// This interface will be removed after migration is complete.
/// </remarks>
public interface IMailSender
{
    /// <summary>
    /// Sends an email
    /// </summary>
    /// <param name="letter">DTO letter</param>
    /// <returns></returns>
    Task SendAsync(MailLetter letter);
}
