namespace DM.Infrastructure.Mail;

/// <summary>
/// Information about the mail transport
/// </summary>
public static class MailTransport
{
    /// <summary>
    /// MQ exchange every letter is published to and the mail worker consumes
    /// </summary>
    public const string ExchangeName = "dm.mail.sending";
}
