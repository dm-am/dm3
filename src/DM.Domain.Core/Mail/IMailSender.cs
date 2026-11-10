namespace DM.Domain.Core.Mail;

/// <summary>
/// Sends email messages
/// </summary>
public interface IMailSender
{
    Task SendAsync(EmailLetter letter);
}
