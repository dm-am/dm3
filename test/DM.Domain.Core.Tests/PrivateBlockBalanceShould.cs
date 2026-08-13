using DM.Domain.Core.Content;
using FluentAssertions;
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
    /// Null is what an edit that does not touch the text hands in.
    /// </summary>
    [Fact]
    public void AcceptNothingToCheck()
    {
        PrivateBlockMarkup.IsBalanced(null).Should().BeTrue();
    }
}
