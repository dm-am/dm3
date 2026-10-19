using System.Text.Encodings.Web;
using System.Text.Unicode;
using DM.Domain.Account.Features.Mail;

namespace DM.Workers.NotificationDispatcher.Dispatching;

/// <summary>
/// The escaping that puts the words of a notification into a message.
/// </summary>
internal static class NotificationText
{
    /// <summary>
    /// Encoder every value that becomes markup goes through.
    /// </summary>
    /// <remarks>
    /// The default encoder escapes every Cyrillic letter; Telegram reads the four entities this one emits.
    /// </remarks>
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    /// <summary>What the title of a bot message starts with: the words a letter's subject opens with.</summary>
    public const string TitlePrefix = MailSubject.Prefix;

    /// <summary>
    /// Text on its way into HTML: the Telegram message.
    /// </summary>
    /// <param name="text">Text that has to arrive as text</param>
    public static string EscapeHtml(string text) => Encoder.Encode(text);
}
