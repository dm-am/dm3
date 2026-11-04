using DM.Infrastructure.Persistence.Repositories.Search;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Search;

/// <summary>
/// What a search preview may show of a post that has private blocks in it.
/// </summary>
/// <remarks>
/// Snippets are built past the viewer-scoped render, so this cut is the lock; PrivateBlockPatternShould
/// asserts PostgreSQL reads the same pattern the same way.
/// </remarks>
public class PrivateBlockSnippetShould
{
    [Theory]
    [InlineData("до [private]тайна[/private] после", "до после")]
    [InlineData("[private=\"Вася, Петя\"]тайна[/private] хвост", " хвост")]
    // Every spelling the renderer reads as a hidden block, in any casing.
    [InlineData("[private=Вася]тайна[/private] хвост", " хвост")]
    [InlineData("до [PRIVATE]тайна[/private] после", "до после")]
    [InlineData("[private to=\"Вася\"]тайна[/private] хвост", " хвост")]
    [InlineData("[private\n=\"Вася\"]тайна[/private] хвост", " хвост")]
    // The closing tag in any casing too, as on the page.
    [InlineData("до [private]тайна[/PRIVATE] и хвост", "до и хвост")]
    [InlineData("до [Private]тайна[/pRiVaTe] и хвост", "до и хвост")]
    public void RemoveThePrivateBlockAndCollapseWhatIsLeft(string input, string expected)
    {
        SearchSnippet.StripPrivateBlocks(input).Should().Be(expected);
    }

    /// <summary>
    /// Only [/private] closes a block, as on the page: a block closed by anything
    /// else runs to the end of the text, and so does one that was never closed.
    /// </summary>
    [Theory]
    [InlineData("до [private]тайна[/private=\"x\"] и хвост")]
    [InlineData("до [private]тайна[/pr\u0131vate] и хвост")]
    [InlineData("до [private=\"Вася\"]без закрытия и хвост")]
    [InlineData("до [private")]
    public void RemoveTheRestOfTheTextWhenNothingClosesTheBlock(string input)
    {
        SearchSnippet.StripPrivateBlocks(input).Should().Be("до ");
    }

    [Theory]
    [InlineData("privatebank и [privateer] тоже")]
    [InlineData("[private_x] остается")]
    public void KeepAWordThatOnlyStartsLikeTheTag(string input)
    {
        SearchSnippet.StripPrivateBlocks(input).Should().Be(input);
    }

    /// <summary>
    /// Text standing between two private blocks is public and stays, here and in PostgreSQL.
    /// </summary>
    [Fact]
    public void KeepPublicTextBetweenTwoBlocks()
    {
        SearchSnippet.StripPrivateBlocks("A [private]x[/private] СЕРЕДИНА [private]y[/private] B")
            .Should().Be("A СЕРЕДИНА B");
    }
}
