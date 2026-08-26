using AwesomeAssertions;
using DM.Infrastructure.Core.Parsing;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// Text an author writes inside a list but outside its items, on the three
/// outputs that carry it.
/// </summary>
/// <remarks>
/// It used to reach only two of them. The HTML path dropped the text children
/// of a list outright, while the source and the text projection kept them, so
/// the author saw their paragraph every time they opened the post for editing
/// and no reader ever saw it on the page. Nothing reported the loss - the two
/// outputs that agreed were the two nobody compares.
///
/// The rule that replaced the dropping is one sentence: the list markup wraps
/// runs of items and steps aside for anything else, so the text is printed
/// where it was written and a ul still contains only li. The reasoning is in
/// TagNode.EmitList; the cases below are what it is pinned on. The parser
/// comes from BbParserProvider rather than a set built here, because the
/// question these ask is what a reader of a comment gets, not what the walk
/// does in isolation - that one is BbTreeWalkShould.
/// </remarks>
public class BbListTextShould
{
    private readonly IBbParserProvider parserProvider = new BbParserProvider();

    private string ToHtml(string bbCode) =>
        ((BbParserWrapper.WrappedNodeTree)parserProvider.GetForSurface(BbSurface.Comment).Parse(bbCode)).ToHtml();

    private string ToText(string bbCode) =>
        ((BbParserWrapper.WrappedNodeTree)parserProvider.GetForSurface(BbSurface.Comment).Parse(bbCode)).ToText();

    private string ToBb(string bbCode) =>
        ((BbParserWrapper.WrappedNodeTree)parserProvider.GetForSurface(BbSurface.Comment).Parse(bbCode)).ToBb();

    /// <summary>
    /// The defect itself: this text reached the source and the text projection
    /// and never reached the page.
    /// </summary>
    [Fact]
    public void PrintTextWrittenBetweenItems()
    {
        ToHtml("[ul][li]раз[/li]а вот и абзац[li]два[/li][/ul]")
            .Should().Be("<ul><li>раз</li></ul>а вот и абзац<ul><li>два</li></ul>");
    }

    /// <summary>
    /// The same in an ordered list, where the split costs the numbering: the
    /// second chunk starts from one again. That is the price of showing the
    /// text, and it is named here so it is not mistaken for a defect later.
    /// </summary>
    [Fact]
    public void SplitAnOrderedListToo_AtTheCostOfItsNumbering()
    {
        ToHtml("[ol][li]раз[/li]а вот и абзац[li]два[/li][/ol]")
            .Should().Be("<ol><li>раз</li></ol>а вот и абзац<ol><li>два</li></ol>");
    }

    [Fact]
    public void PrintTextWrittenBeforeTheFirstItem_AheadOfTheList()
    {
        ToHtml("[ul]вступление[li]раз[/li][li]два[/li][/ul]")
            .Should().Be("вступление<ul><li>раз</li><li>два</li></ul>");
    }

    [Fact]
    public void PrintTextWrittenAfterTheLastItem_BehindTheList()
    {
        ToHtml("[ul][li]раз[/li][li]два[/li]послесловие[/ul]")
            .Should().Be("<ul><li>раз</li><li>два</li></ul>послесловие");
    }

    /// <summary>
    /// The whitespace that separates one item from the next is layout, not
    /// text, and it has to keep being dropped: the substitution table turns a
    /// newline into a line break, so printing it would break every list ever
    /// written one item per line into a column of blank lines.
    /// </summary>
    [Fact]
    public void DropTheWhitespaceBetweenItems()
    {
        ToHtml("[ul]\n\t[li]раз[/li]\n\t[li]два[/li]\n[/ul]")
            .Should().Be("<ul><li>раз</li><li>два</li></ul>");
    }

    /// <summary>
    /// And the same whitespace around text that is kept comes off with it -
    /// otherwise a paragraph written on its own line, which is how anyone
    /// would write one, arrives fenced by line breaks.
    /// </summary>
    [Fact]
    public void TrimTheWhitespaceAroundTextItPrints()
    {
        ToHtml("[ul]\n[li]раз[/li]\nа вот и абзац\n[li]два[/li]\n[/ul]")
            .Should().Be("<ul><li>раз</li></ul>а вот и абзац<ul><li>два</li></ul>");
    }

    /// <summary>
    /// A nested list is written inside an item, and the text between its items
    /// is lifted only out of it - landing inside that item, where the author
    /// put it, and where an li is allowed to hold text.
    /// </summary>
    [Fact]
    public void LiftTextOutOfTheInnerListOnly()
    {
        ToHtml("[ul][li]раз[ul][li]вложенный[/li]между[li]еще[/li][/ul][/li][li]два[/li][/ul]")
            .Should().Be(
                "<ul><li>раз<ul><li>вложенный</li></ul>между<ul><li>еще</li></ul></li>" +
                "<li>два</li></ul>");
    }

    /// <summary>
    /// A list with nothing in it renders as it always has. There is no text to
    /// save and no item to wrap, and the empty markup is what the previous
    /// walk emitted.
    /// </summary>
    [Fact]
    public void RenderAnEmptyList_AsItAlwaysHas()
    {
        ToHtml("[ul][/ul]").Should().Be("<ul></ul>");
    }

    /// <summary>
    /// A list of nothing but text has no item to wrap, so the list markup is
    /// not emitted at all: an empty box drawn around text standing outside it
    /// is not what the author wrote.
    /// </summary>
    [Fact]
    public void EmitNoListMarkup_WhenTheListHoldsOnlyText()
    {
        ToHtml("[ul]никаких пунктов[/ul]").Should().Be("никаких пунктов");
    }

    /// <summary>
    /// A list written the way another engine spells one. [*] is a tag on
    /// neither side here, so all of this is text, and text is now what comes
    /// out - where the whole line used to vanish, list and items together.
    /// Making [*] work is a separate question with a separate answer.
    /// </summary>
    [Fact]
    public void PrintAListWrittenWithStars_AsThePlainTextItIs()
    {
        ToHtml("[ul][*]раз[*]два[/ul]").Should().Be("[*]раз[*]два");
    }

    /// <summary>
    /// The two outputs that were already keeping this text keep it, in place
    /// and untouched by the HTML rule. The source is what the author gets back
    /// on the next edit, and the text projection is what search reads.
    /// </summary>
    [Fact]
    public void KeepTheTextInTheSource_AndInTheTextProjection()
    {
        const string bbCode = "[ul]вступление[li]раз[/li]а вот и абзац[li]два[/li]послесловие[/ul]";

        ToBb(bbCode).Should().Be(bbCode);
        ToText(bbCode).Should().Be("вступление" + "раз" + "а вот и абзац" + "два" + "послесловие");
    }
}
