using System.IO;
using DM.Domain.Core.Mail;
using MimeKit;
using MimeKit.Text;

namespace DM.Workers.Mail.Sending;

/// <summary>
/// The MIME body of a letter: the HTML as it was rendered, and the same letter
/// in plain text beside it.
/// </summary>
/// <remarks>
/// Text first, HTML last: a client shows the last part it can draw.
/// </remarks>
internal static class LetterBody
{
    /// <summary>The body of the message that carries this letter.</summary>
    public static MimeEntity Build(EmailLetter letter)
    {
        var text = new TextPart(TextFormat.Plain) { Text = PlainTextLetter.From(letter.Body) };

        return new MultipartAlternative
        {
            text,
            Html(letter)
        };
    }

    /// <summary>The HTML half, with the images it refers to when it has any.</summary>
    private static MimeEntity Html(EmailLetter letter)
    {
        var html = new TextPart(TextFormat.Html) { Text = letter.Body };

        if (letter.LinkedResources.Count == 0)
        {
            return html;
        }

        var related = new MultipartRelated { html };

        foreach (var resource in letter.LinkedResources)
        {
            related.Add(new MimePart(resource.MimeType)
            {
                Content = new MimeContent(new MemoryStream(resource.Content)),
                ContentId = resource.ContentId,
                ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
                ContentTransferEncoding = ContentEncoding.Base64
            });
        }

        return related;
    }
}
