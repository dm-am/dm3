using System;
using System.IO;
using System.Text;
using MimeKit.Text;

namespace DM.Workers.Mail.Sending;

/// <summary>
/// The plain text half of a letter, read off its HTML half.
/// </summary>
/// <remarks>
/// One reading for every letter, and not a text template beside each HTML one:
/// a second template per letter is a second copy of the words, and the copy is
/// what drifts. The HTML is the letter; this only says it without the markup.
///
/// Built here and not where the letter is rendered because this is where the
/// message is assembled, and the queue keeps carrying one body: a letter
/// published before this existed is sent with its text half all the same.
///
/// What the markup of a letter can hold is small and known, because the templates
/// are this repository's own: a heading, paragraphs, a box with a table of
/// facts, the button with the links to the other doors of the site under it, a
/// rule and the footnote under it. The reading is generic over those shapes and
/// knows nothing of any one letter:
/// a block of text stands as a paragraph, a row of a table as "label: value", a
/// link as its words followed by its address (the address is the part a reader
/// without HTML needs, and the words say what it is for), the rule as the
/// signature separator "-- ", so a client that folds signatures folds the
/// footnote. An image says nothing: the only one is the logo.
/// </remarks>
internal static class PlainTextLetter
{
    /// <summary>The text of a letter whose HTML is given.</summary>
    public static string From(string html)
    {
        var page = new Page();
        var tokenizer = new HtmlTokenizer(new StringReader(html)) { DecodeCharacterReferences = true };

        // The link being read: its words are gathered until it closes, because
        // the address goes after them.
        string? href = null;
        StringBuilder? words = null;
        var cellsInRow = 0;

        while (tokenizer.ReadNextToken(out var token))
        {
            switch (token)
            {
                case HtmlDataToken data when words != null:
                    words.Append(data.Data);
                    break;

                case HtmlDataToken data:
                    page.Write(data.Data);
                    break;

                case HtmlTagToken { Id: HtmlTagId.A, IsEndTag: false } anchor:
                    href = AttributeOf(anchor, HtmlAttributeId.Href);
                    words = new StringBuilder();
                    break;

                case HtmlTagToken { Id: HtmlTagId.A, IsEndTag: true }:
                    page.Link(words?.ToString() ?? string.Empty, href);
                    href = null;
                    words = null;
                    break;

                case HtmlTagToken { Id: HtmlTagId.Br }:
                    page.Break(Page.Line);
                    break;

                case HtmlTagToken { Id: HtmlTagId.HR }:
                    page.Separator();
                    break;

                case HtmlTagToken { Id: HtmlTagId.TR, IsEndTag: false }:
                    cellsInRow = 0;
                    page.Break(Page.Line);
                    break;

                case HtmlTagToken { Id: HtmlTagId.TD or HtmlTagId.TH, IsEndTag: false }:
                    if (cellsInRow++ > 0) page.Exactly(": ");
                    break;

                case HtmlTagToken { Id: HtmlTagId.TR or HtmlTagId.TD or HtmlTagId.TH }:
                    break;

                case HtmlTagToken tag when IsBlock(tag.Id):
                    page.Break(Page.Paragraph);
                    break;
            }
        }

        return page.ToString();
    }

    private static bool IsBlock(HtmlTagId id) => id is
        HtmlTagId.P or HtmlTagId.Div or HtmlTagId.Table or HtmlTagId.Body or
        HtmlTagId.H1 or HtmlTagId.H2 or HtmlTagId.H3 or HtmlTagId.H4 or HtmlTagId.H5 or HtmlTagId.H6 or
        HtmlTagId.UL or HtmlTagId.OL or HtmlTagId.LI or HtmlTagId.BlockQuote;

    private static string? AttributeOf(HtmlTagToken tag, HtmlAttributeId id)
    {
        foreach (var attribute in tag.Attributes)
        {
            if (attribute.Id == id) return attribute.Value;
        }

        return null;
    }

    /// <summary>
    /// The text as it is written: words with the whitespace of the markup folded
    /// the way a browser folds it, and the breaks the blocks owe in between.
    /// </summary>
    private sealed class Page
    {
        /// <summary>A new line</summary>
        public const int Line = 1;

        /// <summary>An empty line between two blocks</summary>
        public const int Paragraph = 2;

        private readonly StringBuilder _text = new();
        private int _breaks;
        private bool _space;

        /// <summary>The separator was the last thing written, and binds the next line to it.</summary>
        private bool _separated;

        public void Break(int lines) => _breaks = _separated ? Line : Math.Max(_breaks, lines);

        /// <summary>
        /// The signature separator: a line of its own, the footnote right under it.
        /// </summary>
        public void Separator()
        {
            Break(Paragraph);
            Exactly("-- ");
            _breaks = Line;
            _separated = true;
        }

        public void Write(string words)
        {
            foreach (var character in words)
            {
                if (char.IsWhiteSpace(character))
                {
                    _space = true;
                    continue;
                }

                Settle(spaced: true);
                _text.Append(character);
            }
        }

        /// <summary>Text that keeps its own spacing, the separator above all.</summary>
        public void Exactly(string text)
        {
            Settle(spaced: false);
            _text.Append(text);
        }

        /// <summary>
        /// A link: its words, then its address. An address standing as its own
        /// words is said once.
        /// </summary>
        public void Link(string words, string? href)
        {
            var said = Collapse(words);
            if (string.IsNullOrWhiteSpace(href))
            {
                Write(said);
                return;
            }

            if (said.Length > 0 && said != href)
            {
                Write(said);
                Exactly(": ");
            }

            Exactly(href);
        }

        public override string ToString() => _text.Length == 0 ? string.Empty : _text + "\n";

        /// <summary>Pays the breaks and the space owed before the next character.</summary>
        private void Settle(bool spaced)
        {
            if (_text.Length > 0)
            {
                if (_breaks > 0) _text.Append('\n', _breaks);
                else if (_space && spaced && _text[^1] != ' ') _text.Append(' ');
            }

            _breaks = 0;
            _space = false;
            _separated = false;
        }

        private static string Collapse(string words) =>
            string.Join(' ', words.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
