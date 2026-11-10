using AwesomeAssertions;
using DM.Infrastructure.Core.Parsing;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

// The list markup wraps runs of items and steps aside for anything else, so a ul holds only li (see
// TagNode.EmitList).
public class BbListTextShould
{
    private readonly IBbParserProvider parserProvider = new BbParserProvider();

    private string ToHtml(string bbCode) =>
        parserProvider.GetForSurface(BbSurface.Comment).Parse(bbCode).ToHtml();

    private string ToText(string bbCode) =>
        parserProvider.GetForSurface(BbSurface.Comment).Parse(bbCode).ToText();

    [Fact]
    public void PrintTextWrittenBetweenItems()
    {
        ToHtml("[ul][li]раз[/li]а вот и абзац[li]два[/li][/ul]")
            .Should().Be("<ul><li>раз</li></ul>а вот и абзац<ul><li>два</li></ul>");
    }

    // The same in an ordered list, where the second chunk numbers from one again: the price of
    // showing the text.
    [Fact]
    public void SplitAnOrderedListTooAtTheCostOfItsNumbering()
    {
        ToHtml("[ol][li]раз[/li]а вот и абзац[li]два[/li][/ol]")
            .Should().Be("<ol><li>раз</li></ol>а вот и абзац<ol><li>два</li></ol>");
    }

    [Fact]
    public void PrintTextWrittenBeforeTheFirstItemAheadOfTheList()
    {
        ToHtml("[ul]вступление[li]раз[/li][li]два[/li][/ul]")
            .Should().Be("вступление<ul><li>раз</li><li>два</li></ul>");
    }

    [Fact]
    public void PrintTextWrittenAfterTheLastItemBehindTheList()
    {
        ToHtml("[ul][li]раз[/li][li]два[/li]послесловие[/ul]")
            .Should().Be("<ul><li>раз</li><li>два</li></ul>послесловие");
    }

    // The whitespace between items is layout and stays dropped: a newline would become a line
    // break.
    [Fact]
    public void DropTheWhitespaceBetweenItems()
    {
        ToHtml("[ul]\n\t[li]раз[/li]\n\t[li]два[/li]\n[/ul]")
            .Should().Be("<ul><li>раз</li><li>два</li></ul>");
    }

    [Fact]
    public void TrimTheWhitespaceAroundTextItPrints()
    {
        ToHtml("[ul]\n[li]раз[/li]\nа вот и абзац\n[li]два[/li]\n[/ul]")
            .Should().Be("<ul><li>раз</li></ul>а вот и абзац<ul><li>два</li></ul>");
    }

    [Fact]
    public void LiftTextOutOfTheInnerListOnly()
    {
        ToHtml("[ul][li]раз[ul][li]вложенный[/li]между[li]еще[/li][/ul][/li][li]два[/li][/ul]")
            .Should().Be(
                "<ul><li>раз<ul><li>вложенный</li></ul>между<ul><li>еще</li></ul></li>" +
                "<li>два</li></ul>");
    }

    [Fact]
    public void RenderAnEmptyListAsItAlwaysHas()
    {
        ToHtml("[ul][/ul]").Should().Be("<ul></ul>");
    }

    [Fact]
    public void EmitNoListMarkupWhenTheListHoldsOnlyText()
    {
        ToHtml("[ul]никаких пунктов[/ul]").Should().Be("никаких пунктов");
    }

    [Fact]
    public void PrintAListWrittenWithStarsAsThePlainTextItIs()
    {
        ToHtml("[ul][*]раз[*]два[/ul]").Should().Be("[*]раз[*]два");
    }

    /// <summary>
    /// The text projection keeps this text in place: it is what search reads.
    /// </summary>
    [Fact]
    public void KeepTheTextInTheTextProjection()
    {
        const string bbCode = "[ul]вступление[li]раз[/li]а вот и абзац[li]два[/li]послесловие[/ul]";

        ToText(bbCode).Should().Be("вступление" + "раз" + "а вот и абзац" + "два" + "послесловие");
    }
}
