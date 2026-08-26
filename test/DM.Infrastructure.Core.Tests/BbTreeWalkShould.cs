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
/// The walk was rewritten from three recursive renderers into one iterative
/// one, and the only thing that made that rewrite safe was that the output did
/// not move. Nothing was checking that: the resilience tests assert lengths on a
/// tree of identical nodes, and a length says nothing about order - an
/// implementation that emitted every closing tag before its children would
/// satisfy it to the byte.
///
/// The expected strings below were produced by the recursive renderer, running
/// on this fixture before the rewrite. They are a record of what it did, not a
/// judgement of what it should do: the fixture deliberately avoids the two
/// spellings that were meant to change (an unquoted attribute, and nesting past
/// the depth guard), so any difference here is a regression rather than an
/// intention.
///
/// One expectation has since been moved on purpose, and it is the only one.
/// The recursive renderer dropped the text children of a list on the HTML path,
/// so "мимо" and "тоже мимо" - words their author typed and got back, whole, on
/// every later edit - reached no reader. The HTML expectation below carried that
/// silence as a fact: one ul, the two items, and nothing between or around
/// them. Now the words are there, and the list markup
/// steps aside for them rather than swallowing them or wrapping them into
/// invented items: text before the first item comes out ahead of the list, text
/// between two items splits it, and each ul still holds only li. See
/// TagNode.EmitList for why that shape and not another, and
/// BbListTextShould for the cases the rule is pinned on. The other two
/// expectations are untouched - the text projection and the source were keeping
/// those words all along, and this fixture is what says they still do.
///
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
/// - security substitutions in ordinary text, and their absence from ToBb.
/// </remarks>
public class BbTreeWalkShould
{
    private const string Fixture =
        "[quote=\"Автор\"][b]жирный[/b] -- текст[/quote]\n" +
        "[ul]мимо[li]раз[/li]тоже мимо[li]два[/li][/ul]" +
        "[code][b]не тег[/b] < & >[/code]" +
        "[pre]a -- b[/pre][empty][/empty][tab]a < b & c";

    [Fact]
    public void EmitTheSameHtml_AsTheRecursiveWalkDid_SaveForTheListTextItDropped()
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
    public void EmitTheSameText_AsTheRecursiveWalkDid()
    {
        Parse().ToText().Should().Be(
            "жирный &ndash; текст<br />" +
            "миморазтоже мимодва" +
            "[b]не тег[/b] &lt; &amp; &gt;" +
            "a -- b" +
            "a &lt; b &amp; c");
    }

    [Fact]
    public void EmitTheSameBbCode_AsTheRecursiveWalkDid()
    {
        Parse().ToBb().Should().Be(
            "[quote=\"Автор\"][b]жирный[/b] -- текст[/quote]\n" +
            "[ul]мимо[li]раз[/li]тоже мимо[li]два[/li][/ul]" +
            "[code][b]не тег[/b] < & >[/code]" +
            "[pre]a -- b[/pre][empty][/empty][tab]a < b & c");
    }

    /// <summary>
    /// The tag set is built here rather than taken from BbParserProvider: this
    /// is a test of the walk, and a set that changes for product reasons would
    /// move the expected strings for reasons that have nothing to do with it.
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

        return new BbParser(tags, BbParser.SecuritySubstitutions, aliases).Parse(Fixture);
    }
}
