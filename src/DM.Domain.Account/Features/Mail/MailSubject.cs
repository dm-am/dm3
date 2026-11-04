namespace DM.Domain.Account.Features.Mail;

/// <summary>
/// The subject of every letter the site sends, and of every bot message.
/// </summary>
/// <remarks>
/// A mailbox filter matches the start of a subject, so the site is named there and only there.
/// Public, because the notification dispatcher opens its letters and messages with the same words.
/// </remarks>
public static class MailSubject
{
    /// <summary>What every subject starts with</summary>
    public const string Prefix = "Dungeon Master: ";

    /// <summary>The subject of a letter about this errand</summary>
    public static string Of(string topic) => Prefix + topic;
}
