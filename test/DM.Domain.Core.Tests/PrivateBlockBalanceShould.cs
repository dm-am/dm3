using DM.Domain.Core.Content;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests;

/// <summary>
/// A private block that is not a block is refused before it is stored.
/// </summary>
/// <remarks>
/// Everything downstream — the stored search vector, the room search filter, the
/// snippet on the results page — cuts private text out by matching a whole block.
/// An unclosed tag matches nothing, so the text after it is indexed, searchable
/// and shown in a preview, while the page itself renders it as private and hides
/// it: the author sees a line nobody should read, and search shows it to
/// everybody. A block opened inside another is the same failure from the other
/// side — the cut stops at the first closing tag and the rest of the outer block
/// comes back out as public text.
/// </remarks>
public class PrivateBlockBalanceShould
{
    [Theory]
    [InlineData("")]
    [InlineData("ничего особенного")]
    [InlineData("до [private]тайна[/private] после")]
    [InlineData("[private=\"Вася\"]раз[/private] и [private=\"Петя\"]два[/private]")]
    [InlineData("[private=\"\"]без имени[/private]")]
    [InlineData("[private=\"Вася, Петя\"]список[/private]")]
    // A name is the same name in any case, the closing tag's included.
    [InlineData("[private]тайна[/PRIVATE] хвост")]
    [InlineData("[PRIVATE]тайна[/private]")]
    [InlineData("[Private=\"Вася\"]тайна[/Private]")]
    public void AcceptTextWhereEveryBlockIsClosed(string source)
    {
        PrivateBlockMarkup.IsBalanced(source).Should().BeTrue();
    }

    [Theory]
    // Opened and never closed: the text after it is indexed as public.
    [InlineData("до [private]без закрытия")]
    [InlineData("[private=\"Вася\"]раз[/private] и [private]два")]
    // Closed only by a spelling that closes nothing: the block runs on.
    [InlineData("[private]тайна[/private=\"x\"] хвост")]
    // Closed without being opened: the tag renders as text and means nothing.
    [InlineData("текст [/private] дальше")]
    // Opened inside itself: the cut ends at the first closing tag.
    [InlineData("[private]внешний [private]внутренний[/private] хвост[/private]")]
    public void RefuseTextWhereABlockIsNotABlock(string source)
    {
        PrivateBlockMarkup.IsBalanced(source).Should().BeFalse();
    }

    /// <summary>
    /// Hiding markup written after a [noparse] the author never closed.
    /// </summary>
    /// <remarks>
    /// The count agrees with itself here — one opening tag, one closing tag —
    /// and the page reads the block as a block, because a [noparse] nobody
    /// closed is no example and what follows it is read as any other text. The
    /// author almost always wrote an example and forgot the closing tag, and
    /// without a refusal that example would quietly become a secret: a block the
    /// author did close is a written instruction to show the markup literally,
    /// and an unclosed one carries no instruction at all, so the save path
    /// refuses and asks. The name of the block is a name in any case.
    /// </remarks>
    [Theory]
    [InlineData("[noparse][private=\"Чак\"]СЕКРЕТ[/private]")]
    [InlineData("[quote][noparse]пример[/quote] [private=\"Чак\"]СЕКРЕТ[/private]")]
    [InlineData("до [noparse] и [private=Вася]тайна")]
    [InlineData("[NOPARSE][private]тайна[/private]")]
    [InlineData("[NoParse]пример [b]жирно[/b] [private=\"Чак\"]СЕКРЕТ[/private] хвост")]
    public void RefuseHidingMarkupAfterANoparseThatIsNotClosed(string source)
    {
        PrivateBlockMarkup.IsBalanced(source).Should().BeFalse();
    }

    /// <summary>
    /// [code] is no example: it changes the font, as on DM2, and a hidden block
    /// inside it, closed or not, is read as a block and judged as one.
    /// </summary>
    /// <remarks>
    /// It used to be read as [noparse] is. A block inside a closed [code] was an
    /// example the count skipped, and one after a [code] nobody closed was
    /// refused as a forgotten example.
    /// </remarks>
    [Theory]
    [InlineData("[code][private=\"Чак\"]СЕКРЕТ[/private]", true)]
    [InlineData("[Code]пример [b]жирно[/b] [private=\"Чак\"]СЕКРЕТ[/private] хвост", true)]
    [InlineData("[code][private=\"Чак\"]СЕКРЕТ[/private][/code]", true)]
    [InlineData("[code][private=\"Чак\"]пример[/code]", false)]
    [InlineData("[CODE][private=\"Чак\"]пример[/Code]", false)]
    [InlineData("[code][/private][/code]", false)]
    public void ReadHidingMarkupInsideACodeAsMarkup(string source, bool accepted)
    {
        PrivateBlockMarkup.IsBalanced(source).Should().Be(accepted);
    }

    /// <summary>
    /// A [noparse] nobody closed hides nothing and shows nothing as an example,
    /// so the blocks after it are blocks, and their addressees are frozen like
    /// any others.
    /// </summary>
    /// <remarks>
    /// The save path refuses such a text, and a text that never went through it
    /// is still read by the snapshot of an edit: every block the page reads as
    /// addressed has to be in it, not the first one alone.
    /// </remarks>
    [Fact]
    public void ReadEveryBlockAfterAVerbatimBlockThatIsNotClosed()
    {
        PrivateBlockMarkup.ReadAddresseeAttributes(
                "[noparse]пример [private=\"Вася\"]раз[/private] и [private=\"Петя\"]два[/private]")
            .Should().Equal("Вася", "Петя");
    }

    /// <summary>
    /// A block inside a closed [code] is a block on the page, so its addressees
    /// are frozen as well; one inside a closed [noparse] is an example and names
    /// nobody.
    /// </summary>
    [Fact]
    public void ReadTheBlockInsideACode_AndNotTheOneInsideANoparse()
    {
        PrivateBlockMarkup.ReadAddresseeAttributes(
                "[code][private=\"Вася\"]раз[/private][/code] [noparse][private=\"Петя\"]два[/private][/noparse]")
            .Should().Equal("Вася");
    }

    /// <summary>
    /// The same markup inside a verbatim block the author did close is an
    /// example of how to write it, and the count must not read it as a block.
    /// </summary>
    /// <remarks>
    /// Nothing inside a closed verbatim block is ever a node, so nothing inside
    /// it can hide anything. Counting it refused the one way the product has of
    /// teaching its own syntax.
    /// </remarks>
    [Theory]
    [InlineData("[noparse][private=Чак]пример[/noparse]")]
    [InlineData("[noparse][private=\"Чак\"]пример[/noparse]")]
    [InlineData("[noparse][/private][/noparse]")]
    [InlineData("[noparse][private]пример[/noparse] и [private=\"Вася\"]тайна[/private]")]
    [InlineData("[NOPARSE][private=\"Чак\"]пример[/NoParse]")]
    [InlineData("[code][noparse][private=\"Чак\"]пример[/private][/noparse][/code]")]
    public void AcceptHidingMarkupShownAsAnExampleInAClosedVerbatimBlock(string source)
    {
        PrivateBlockMarkup.IsBalanced(source).Should().BeTrue();
    }

    /// <summary>
    /// A block the grammar does not accept is refused, and the refusal quotes
    /// the spelling, names its place and shows the right one.
    /// </summary>
    /// <remarks>
    /// On the page such a block is a block that addresses nobody, which the
    /// author did not mean: the unquoted name among them is the form the editor
    /// used to write, and a name read out of it would be a guess at where the
    /// name ends.
    /// </remarks>
    [Theory]
    [InlineData("[private=Вася]тайна[/private]", "[private=Вася]")]
    [InlineData("[private=\"Вася]тайна[/private]", "[private=\"Вася]")]
    [InlineData("[private=Вася\"]тайна[/private]", "[private=Вася\"]")]
    [InlineData("[private= \"Вася\"]тайна[/private]", "[private= \"Вася\"]")]
    [InlineData("[private =\"Вася\"]тайна[/private]", "[private =\"Вася\"]")]
    [InlineData("[private to=\"Вася\"]тайна[/private]", "[private to=\"Вася\"]")]
    [InlineData("[PRIVATE=Вася]тайна[/private]", "[PRIVATE=Вася]")]
    [InlineData("[private]", "[private]")]
    [InlineData("текст [private", "[private")]
    public void RefuseAMisspelledBlock_AndSayHowToWriteIt(string source, string spelling)
    {
        PrivateBlockMarkup.IsBalanced(source).Should().BeFalse();

        var place = source.IndexOf('[', System.StringComparison.Ordinal) + 1;
        var refusal = PrivateBlockMarkup.DescribeBalanceRefusal(source);
        refusal.Should().Contain(spelling).And.Contain($"символ {place}");
        if (spelling != "[private]")
        {
            refusal.Should().Contain("[private=\"Имя\"]").And.Contain("[private] без имени");
        }
    }

    /// <summary>
    /// A block closed only by a misspelled closing tag is refused at that tag:
    /// it is the one the author meant to close the block with, and the place to
    /// fix. The editor's check names the same tag.
    /// </summary>
    [Theory]
    [InlineData("[private]тайна[/private=\"x\"] хвост", "[/private=\"x\"]")]
    [InlineData("[private=\"Вася\"]тайна[/PRIVATE =\"x\"] и [/private =\"y\"] хвост", "[/PRIVATE =\"x\"]")]
    public void NameTheMisspelledClosingTag_WhenItLeavesTheBlockOpen(string source, string spelling)
    {
        var refusal = PrivateBlockMarkup.DescribeBalanceRefusal(source);

        refusal.Should().Contain(spelling)
            .And.Contain($"символ {source.IndexOf(spelling, System.StringComparison.Ordinal) + 1}")
            .And.Contain("[/private]");
    }

    /// <summary>
    /// A misspelled closing tag after a block that is properly closed leaves no
    /// block open, so the refusal, if any, is about something else.
    /// </summary>
    [Fact]
    public void KeepTheUnclosedRefusal_WhenTheMisspelledClosingTagStandsOutsideTheBlock()
    {
        const string source = "[private]раз[/private] [/private=\"x\"] [private]два";

        PrivateBlockMarkup.DescribeBalanceRefusal(source)
            .Should().StartWith("Скрытый блок не закрыт: [private], символ 39.");
    }

    /// <summary>
    /// Null is what an edit that does not touch the text hands in.
    /// </summary>
    [Fact]
    public void AcceptNothingToCheck()
    {
        PrivateBlockMarkup.IsBalanced(null).Should().BeTrue();
    }
}
