using System.Collections.Generic;
using AwesomeAssertions;
using BBCodeParser;
using BBCodeParser.Tags;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// The three outputs of the tree walk, byte for byte, on one fixture that
/// carries every rule the walk has.
/// </summary>
/// <remarks>
/// The walk is iterative; the expected strings pin its output byte for byte, order included, which a
/// length check cannot. Text between list items is lifted out of the list markup in HTML (see
/// TagNode.EmitList and BbListTextShould), and the text output is plain text (see NodeTree.ToText).
/// What the fixture pins, in order of appearance:
/// - an attribute value substituted into both the opening and the closing markup;
/// - a tag nested inside another;
/// - alias substitution in text ("--") and the newline turning into a break;
/// - a list tag with text between its items - lifted out of the list markup in
///   HTML, kept in place in text and in the source;
/// - a code tag, whose subtree is emitted as BBCode source in every mode, with
///   security substitutions still applied;
/// - a preformatted tag, which turns alias substitution off for its subtree;
/// - a tag with no children at all;
/// - a tag that never closes;
/// - security substitutions in ordinary text, and their absence from ToText and
///   ToBb.
/// </remarks>
public class BbTreeWalkShould
{
    private const string Fixture =
        "[quote=\"Автор\"][b]жирный[/b] -- текст[/quote]\n" +
        "[ul]мимо[li]раз[/li]тоже мимо[li]два[/li][/ul]" +
        "[code][b]не тег[/b] < & >[/code]" +
        "[pre]a -- b[/pre][empty][/empty][tab]a < b & c";

    [Fact]
    public void EmitTheFixtureAsHtml()
    {
        Parse().ToHtml().Should().Be(
            "<div class=\"quote\"><div class=\"quote-author\">Автор</div>" +
            "<strong>жирный</strong> &ndash; текст</div>" +
            "<br />" +
            "мимо<ul><li>раз</li></ul>тоже мимо<ul><li>два</li></ul>" +
            "<pre>[b]не тег[/b] &lt; &amp; &gt;</pre>" +
            "<pre class=\"p\">a -- b</pre>" +
            "<span></span>" +
            "&nbsp;a &lt; b &amp; c");
    }

    [Fact]
    public void EmitTheFixtureAsPlainText()
    {
        Parse().ToText().Should().Be(
            "жирный ~ текст\n" +
            "миморазтоже мимодва" +
            "[b]не тег[/b] < & >" +
            "a -- b" +
            "a < b & c");
    }

    [Fact]
    public void EmitTheFixtureAsBbCode()
    {
        Parse().ToBb().Should().Be(
            "[quote=\"Автор\"][b]жирный[/b] -- текст[/quote]\n" +
            "[ul]мимо[li]раз[/li]тоже мимо[li]два[/li][/ul]" +
            "[code][b]не тег[/b] < & >[/code]" +
            "[pre]a -- b[/pre][empty][/empty][tab]a < b & c");
    }

    /// <summary>
    /// The tag set is built here, not taken from BbParserProvider: product changes must not move these strings.
    /// </summary>
    private static BBCodeParser.Nodes.NodeTree Parse()
    {
        var tags = new Tag[]
        {
            new("b", "<strong>", "</strong>"),
            new("quote", "<div class=\"quote\"><div class=\"quote-author\">{value}</div>", "</div>", true, false),
            new ListTag("ul", "<ul>", "</ul>"),
            new("li", "<li>", "</li>"),
            new CodeTag("code", "<pre>", "</pre>"),
            new PreformattedTag("pre", "<pre class=\"p\">", "</pre>"),
            new("empty", "<span>", "</span>"),
            new("tab", "&nbsp;")
        };

        var aliases = new Dictionary<string, string> { { "--", "&ndash;" }, { "\n", "<br />" } };
        var text = new Dictionary<string, string> { { "--", "~" } };

        return new BbParser(tags, BbParser.SecuritySubstitutions, aliases, textSubstitutions: text).Parse(Fixture);
    }
}
