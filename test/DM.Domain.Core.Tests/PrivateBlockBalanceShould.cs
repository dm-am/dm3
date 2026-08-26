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
    [InlineData("[private=Вася]раз[/private] и [private=Петя]два[/private]")]
    [InlineData("[PRIVATE]регистр[/PRIVATE]")]
    [InlineData("[private=\"Вася, Петя\"]список[/private]")]
    public void AcceptTextWhereEveryBlockIsClosed(string source)
    {
        PrivateBlockMarkup.IsBalanced(source).Should().BeTrue();
    }

    [Theory]
    // Opened and never closed: the text after it is indexed as public.
    [InlineData("до [private]без закрытия")]
    [InlineData("[private=Вася]раз[/private] и [private]два")]
    // Closed without being opened: the tag renders as text and means nothing.
    [InlineData("текст [/private] дальше")]
    // Opened inside itself: the cut ends at the first closing tag.
    [InlineData("[private]внешний [private]внутренний[/private] хвост[/private]")]
    public void RefuseTextWhereABlockIsNotABlock(string source)
    {
        PrivateBlockMarkup.IsBalanced(source).Should().BeFalse();
    }

    /// <summary>
    /// Hiding markup written after a verbatim block the author never closed.
    /// </summary>
    /// <remarks>
    /// The count agrees with itself here — one opening tag, one closing tag —
    /// and the text still reaches the room. The renderer lifts a verbatim block
    /// out only when it finds the closing tag, so an unclosed one is not lifted:
    /// the [private] inside it goes to the parser as its own tag, the parser
    /// refuses to read anything inside an open verbatim block, and what should
    /// have been a hidden node is plain text served to every reader. Refused
    /// because the block is open: a block the author did close is a written
    /// instruction to show the markup literally, and an unclosed one carries no
    /// instruction at all, so the doubt is resolved towards erasing more.
    /// </remarks>
    [Theory]
    [InlineData("[noparse][private=\"Чак\"]СЕКРЕТ[/private]")]
    [InlineData("[code][private=\"Чак\"]СЕКРЕТ[/private]")]
    [InlineData("[NOPARSE][private]СЕКРЕТ[/private]")]
    [InlineData("до [noparse] и [private=Вася]тайна")]
    public void RefuseHidingMarkupInsideAVerbatimBlockThatIsNotClosed(string source)
    {
        PrivateBlockMarkup.IsBalanced(source).Should().BeFalse();
    }

    /// <summary>
    /// The same markup inside a verbatim block the author did close is an
    /// example of how to write it, and the count must not read it as a block.
    /// </summary>
    /// <remarks>
    /// The renderer cuts a closed verbatim block out before the parse and puts
    /// it back verbatim, so nothing inside it is ever a node and nothing inside
    /// it can hide anything. Counting it refused the one way the product has of
    /// teaching its own syntax.
    /// </remarks>
    [Theory]
    [InlineData("[noparse][private=Чак]пример[/noparse]")]
    [InlineData("[code][private=\"Чак\"]пример[/code]")]
    [InlineData("[noparse][/private][/noparse]")]
    [InlineData("[noparse][private]пример[/noparse] и [private=Вася]тайна[/private]")]
    public void AcceptHidingMarkupShownAsAnExampleInAClosedVerbatimBlock(string source)
    {
        PrivateBlockMarkup.IsBalanced(source).Should().BeTrue();
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
