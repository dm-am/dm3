using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace DM.Workers.NotificationDispatcher.Dispatching;

/// <summary>
/// The escaping that puts the words of a notification into a message.
/// </summary>
/// <remarks>
/// The words themselves are NotificationWording in the domain, shared with the list
/// in the application. Markup is the business of the two channels that write it, the
/// letter and Telegram, so the escaping stays here with them.
/// </remarks>
internal static class NotificationText
{
    /// <summary>
    /// Encoder every value that becomes markup goes through.
    /// </summary>
    /// <remarks>
    /// The same one the Razor templates of DM.Infrastructure.Mail render through, and
    /// for the same reason: the default encoder turns every letter of a Russian name
    /// into a numeric reference. All of Unicode is allowed through, angle brackets,
    /// ampersand and quotes are not. Telegram's HTML parse mode reads what comes out
    /// of it: the four named entities it knows are exactly the ones this encoder
    /// emits, and numeric ones it accepts without exception.
    /// </remarks>
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);

    /// <summary>
    /// Text on its way into HTML: the body of the letter and the Telegram message.
    /// </summary>
    /// <param name="text">Text that has to arrive as text</param>
    public static string EscapeHtml(string text) => Encoder.Encode(text);
}
