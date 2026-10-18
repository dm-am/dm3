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
/// multipart/alternative with the text first and the HTML last, which is the
/// order the standard gives them in: a client shows the last part it can draw,
/// so every client that draws HTML draws exactly what it drew before, and one
/// that cannot, or a reader who asked for text, gets the words and the
/// addresses instead of a raw document. The logo stays where it was, beside
/// the HTML inside multipart/related, because only the HTML refers to it.
///
/// The HTML part is built the way it always was, from the body unchanged: a
/// letter must not look any different for having gained a second half.
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

        // multipart/related for inline images (CID attachments)
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
