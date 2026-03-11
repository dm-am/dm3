namespace DM.Domain.Core.Mail;

/// <summary>
/// Sends email messages
/// </summary>
public interface IMailSender
{
    /// <summary>
    /// Sends an email
    /// </summary>
    /// <param name="letter">Email letter DTO</param>
    /// <returns>Task</returns>
    Task SendAsync(EmailLetter letter);
}
