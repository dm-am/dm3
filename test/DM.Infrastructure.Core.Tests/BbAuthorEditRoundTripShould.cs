using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Infrastructure.Core.Parsing;
using DM.Testing;
using DM.Testing.Dsl;
using DM.Testing.Markup;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// Every tag the author's own editor is filled with comes back out of it as the
/// tag the author wrote.
/// </summary>
/// <remarks>
/// The author_edit audience has one reader: htmlToBbcode in the client, which
/// turns the markup rendered here back into the BBCode the editor is seeded
/// with. The two halves are written in two languages and no compiler sees both,
/// so a template changed on one side broke the round trip without failing
/// anything. [quote] reached the editor as the raw markup it was rendered to,
/// [noparse] lost its wrapper and the example inside became live markup on the
/// next save, and the head of a [spoiler] was saved as a line of text above the
/// block, one more per edit.
///
/// The pairs are written once, in the client's authorEditRoundTrip.spec.ts, and
/// asserted from both ends: here, that each is the markup the server renders;
/// there, that htmlToBbcode gives the BBCode back. A tag a surface declares
/// without a sample fails here as well, so the table cannot fall behind the tag
/// set.
/// </remarks>
public class BbAuthorEditRoundTripShould
{
    private static string PairsFile => Path.Combine(RepositoryLayout.Root,
        "src", "DM.Web.Client", "src", "authorEditRoundTrip.spec.ts");

    /// <summary>
    /// One sample per spelling the editor has to read back, each written the way
    /// htmlToBbcode writes it, so the client can compare its answer as a whole.
    /// </summary>
    private static readonly string[] Samples =
    [
        "[b]текст[/b]",
        "[i]текст[/i]",
        "[u]текст[/u]",
        "[strike]текст[/strike]",
        "[spoiler]текст[/spoiler]",
        "[nsfw]текст[/nsfw]",
        "[quote]текст[/quote]",
        "[ul][li]раз[/li][li]два[/li][/ul]",
        "[ol][li]раз[/li][/ol]",
        "раз[tab]два",
        "[code]a < b[/code]",
        "[noparse][b]текст[/b][/noparse]",
        "[mod]текст[/mod]",
        "[private=\"Вася\"]текст[/private]",
        "[private]текст[/private]",
        "[img]https://example.com/a.png[/img]",
        "[img width=\"100\" height=\"50\"]https://example.com/a.png[/img]",
        "[img height=\"50\"]https://example.com/a.png[/img]",
        "[link]https://example.com/[/link]",
        "[link text=\"текст\"]https://example.com/[/link]",
        "[mention]Вася[/mention]",
        "[spoiler text=\"подпись\"]текст[/spoiler]",
        // Nested, and with text after the inner block: the reverse conversion
        // reads a block up to the first closing tag it meets, so an outer block
        // read before the inner one ends on the inner one's tag and the two come
        // back crossed, as "[spoiler][mod]текст[/spoiler] хвост[/mod]".
        "[spoiler][mod]текст[/mod] хвост[/spoiler]",
        "[spoiler][private=\"Вася\"]текст[/private] хвост[/spoiler]",
        // Plain text the reading typography turns into something else: "--" and
        // "---" into dashes. Neither has a way back, so the author's editor has
        // to be given the text as typed.
        "раз -- два",
        "раз --- два",
        // Found by the generated checks (BbMarkupCorpusShould and the client's
        // bbcodeProperties.spec.ts). Two blocks written side by side came back
        // with a line break between them, one more on every edit. A list came
        // back rearranged: the text between its items lifted out of it, the line
        // breaks between items dropped, a list of text alone without its tags,
        // and an item inside an item split by the browser into three pieces. An
        // empty caption came back as no caption.
        "[quote]раз[/quote][quote]два[/quote]",
        "[ul]раз[li]два[/li]три[/ul]",
        "[ul]\n[li]раз[/li]\n[/ul]",
        "[ol]раз[/ol]",
        "[li]раз[li]два[/li]три[/li]",
        "[spoiler text=\"\"]текст[/spoiler]",
    ];

    private static readonly Regex OpeningTag = new(@"\[(\w+)", RegexOptions.Compiled);

    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    [Fact]
    public void HaveASampleForEveryTagOfEverySurface()
    {
        var sampled = Samples.SelectMany(TagsOf).ToHashSet(StringComparer.Ordinal);

        // The surfaces of content: the hint has no author's editor, a moderator
        // edits its raw string (see BbParserProvider.GetForAuthorEdit).
        var missing = MarkupSurfaces.Content
            .SelectMany(surface => _parserProvider.GetForAuthorEdit(surface).GetTags()
                .Where(tag => !sampled.Contains(tag.Name))
                .Select(tag => $"[{tag.Name}] on {surface}"))
            .Distinct()
            .ToArray();

        string.Join(Environment.NewLine, missing).Should().BeEmpty(
            "a tag the author's editor is filled with and nothing samples is a tag whose way back " +
            "into BBCode nobody checks: add a sample here and its row to authorEditRoundTrip.spec.ts");
    }

    [Fact]
    public void RenderEverySampleAsTheClientTableHoldsIt()
    {
        File.Exists(PairsFile).Should().BeTrue("the client half of the pairs lives there");
        var table = File.ReadAllText(PairsFile);
        var author = new TestSubject { UserId = Guid.NewGuid(), IsAuthenticated = true };

        var missing = new SortedDictionary<string, List<BbSurface>>(StringComparer.Ordinal);
        foreach (var surface in MarkupSurfaces.Content)
        {
            var wrapper = (BbParserWrapper)_parserProvider.GetForAuthorEdit(surface);
            var declared = wrapper.GetTags().Select(tag => tag.Name).ToHashSet(StringComparer.Ordinal);

            foreach (var sample in Samples.Where(sample => TagsOf(sample).All(declared.Contains)))
            {
                var html = wrapper.RenderHtml(sample, RenderContext.ForAuthorEdit(author, surface));
                if (Holds(table, sample, html)) continue;

                var row = $"[{Literal(sample)}, {Literal(html)}],";
                if (!missing.TryGetValue(row, out var surfaces)) missing[row] = surfaces = [];
                surfaces.Add(surface);
            }
        }

        Report(missing).Should().BeEmpty(
            "what the server renders for the author is what htmlToBbcode has to read back, and " +
            "authorEditRoundTrip.spec.ts is where the client is held to it. A row missing there is " +
            "markup the editor has never been shown");
    }

    /// <summary>
    /// The author-edit rows of the grammar table come back to the author as the
    /// text they typed.
    /// </summary>
    /// <remarks>
    /// A row of bbcodeGrammar.cases.ts with the author_edit audience states the
    /// BBCode the editor is given back. The server renders the markup and the
    /// client reads it back, so the row holds when the pairs table pairs that
    /// markup with that BBCode: typography comes back as typed, a broken tag as
    /// the text it is, a parameter through its data-bb-* attribute.
    /// </remarks>
    [Fact]
    public void RenderEveryGrammarRowAsTheClientTableHoldsIt()
    {
        var table = File.ReadAllText(PairsFile);
        var author = new TestSubject { UserId = Guid.NewGuid(), IsAuthenticated = true };

        var rows = BbGrammarTable.Rows.Where(row => row.Audience == "author_edit").ToArray();
        rows.Should().NotBeEmpty("the grammar table states what the author's editor is given back");

        var missing = new SortedDictionary<string, List<BbSurface>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            foreach (var surface in row.Surfaces)
            {
                var wrapper = (BbParserWrapper)_parserProvider.GetForAuthorEdit(surface);
                var html = wrapper.RenderHtml(row.Input, RenderContext.ForAuthorEdit(author, surface));
                if (Holds(table, row.Source, html)) continue;

                var pair = $"[{Literal(row.Source)}, {Literal(html)}],";
                if (!missing.TryGetValue(pair, out var surfaces)) missing[pair] = surfaces = [];
                surfaces.Add(surface);
            }
        }

        Report(missing).Should().BeEmpty(
            "every author_edit row of bbcodeGrammar.cases.ts is markup the server sends the author, and " +
            "authorEditRoundTrip.spec.ts is where htmlToBbcode is held to giving the row's BBCode back");
    }

    /// <summary>
    /// A line break typed as CR LF, or as a CR alone, reaches the author's
    /// editor as one break.
    /// </summary>
    /// <remarks>
    /// The browser turns every CR it parses into LF, so a break written as a CR
    /// and a &lt;br /&gt; was two to the editor, and the first edit of a text
    /// stored with CR LF, the way a DM2 form sent it, doubled every line break
    /// in it. The editor holds no CR, so what it gives back is written with LF,
    /// and the client's table has that pair.
    /// </remarks>
    [Fact]
    public void GiveTheEditorOneBreakForEveryBreakTyped()
    {
        var author = new TestSubject { UserId = Guid.NewGuid(), IsAuthenticated = true };
        var html = ((BbParserWrapper)_parserProvider.GetForAuthorEdit(BbSurface.GamePost))
            .RenderHtml("раз\r\nдва\rтри", RenderContext.ForAuthorEdit(author, BbSurface.GamePost));

        html.Should().Be(BbParserWrapper.LineBreaksWritten + "раз<br />два<br />три");
        Holds(File.ReadAllText(PairsFile), "раз\nдва\nтри", html).Should().BeTrue(
            "authorEditRoundTrip.spec.ts is where htmlToBbcode is held to reading it back");
    }

    private static string Report(SortedDictionary<string, List<BbSurface>> missing) =>
        string.Join(Environment.NewLine,
            missing.Select(row => $"{row.Key} // {string.Join(", ", row.Value)}"));

    private static IEnumerable<string> TagsOf(string sample) =>
        OpeningTag.Matches(sample).Select(match => match.Groups[1].Value);

    /// <summary>
    /// Whether the table holds the pair as one row, in whichever quotes the
    /// formatter put either string in and however it broke the row across lines.
    /// </summary>
    private static bool Holds(string table, string bbCode, string html) => Regex.IsMatch(table,
        $@"\[\s*([""']){Regex.Escape(Escaped(bbCode))}\1\s*,\s*([""']){Regex.Escape(Escaped(html))}\2\s*,?\s*\]");

    /// <summary>The string as the formatter writes it: single quotes when it carries a double one.</summary>
    private static string Literal(string value) =>
        value.Contains('"') ? $"'{Escaped(value)}'" : $"\"{Escaped(value)}\"";

    /// <summary>
    /// A line break as a string literal spells it: a quoted string cannot hold the
    /// break itself.
    /// </summary>
    private static string Escaped(string value) => value.Replace("\n", "\\n");
}
