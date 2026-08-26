using DM.Domain.Core.Content;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests;

/// <summary>
/// Hiding markup is refused where it cannot hide, and the refusal says enough
/// to act on.
/// </summary>
/// <remarks>
/// Only the game post declares [private]. Everywhere else the tag is not markup
/// at all: nothing removes it, nothing hides what it wraps, and the line the
/// author wrote for one reader is published to everyone with the tag still
/// around it. The predicate lives beside the tag's shape rather than beside the
/// tag catalogue, because the catalogue is infrastructure and a validator may
/// not reach into it — a validator knows its own surface by being the validator
/// of that surface.
///
/// The wording matters as much as the refusal. A bare error code names neither
/// the tag nor its place, and an author who wrote a long text is left to find
/// the offending line by bisection; on a surface that never had the tag they
/// are also left without the one thing they need to hear, which is where the
/// tag does work and how to write it as an example.
/// </remarks>
public class PrivateMarkupRefusalShould
{
    [Theory]
    [InlineData("до [private=\"Чак\"]СЕКРЕТ[/private] после")]
    [InlineData("[private]тайна[/private]")]
    [InlineData("[PRIVATE=Вася]тайна[/private]")]
    // Never closed, and never opened: half a tag is an author trying to hide.
    [InlineData("[private=Вася]тайна")]
    [InlineData("хвост [/private]")]
    // The attribute was not terminated, so no well-formed pattern reads it.
    [InlineData("[private=\"Чак]СЕКРЕТ")]
    // Written after a verbatim block nobody closed, which is not an example.
    [InlineData("[noparse][private=\"Чак\"]СЕКРЕТ[/private]")]
    public void FindHidingMarkupWhereverTheParserWouldReadIt(string source)
    {
        PrivateBlockMarkup.ContainsPrivateMarkup(source).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("обычный текст")]
    // The word in prose is not a tag: the name has to be followed by a bracket,
    // an equals sign, a space or the end of the text.
    [InlineData("privatebank и [privateer] тоже")]
    // A closed verbatim block is a written instruction to show this literally.
    [InlineData("[noparse][private=Чак]пример[/noparse]")]
    [InlineData("[code][/private][/code]")]
    public void PassTextThatHidesNothing(string? source)
    {
        PrivateBlockMarkup.ContainsPrivateMarkup(source).Should().BeFalse();
    }

    /// <summary>
    /// The refusal on a surface that does not declare the tag names four
    /// things: the spelling, its place, where the tag works and how to show it
    /// as an example.
    /// </summary>
    [Fact]
    public void TellTheAuthorWhatWasReadAndWhereItWorks()
    {
        var refusal = PrivateBlockMarkup.DescribeSurfaceRefusal("до [private=\"Чак\"]СЕКРЕТ[/private] после");

        refusal.Should().Contain("[private=\"Чак\"]");
        refusal.Should().Contain("символ 4");
        refusal.Should().Contain("игровом посте");
        refusal.Should().Contain("[noparse]");
    }

    /// <summary>
    /// The content of the block is not quoted back. It has not been published
    /// yet, and the refusal is not the place to start.
    /// </summary>
    [Fact]
    public void QuoteTheTagAndNotTheSecret()
    {
        PrivateBlockMarkup.DescribeSurfaceRefusal("до [private=\"Чак\"]СЕКРЕТ[/private] после")
            .Should().NotContain("СЕКРЕТ");
    }

    /// <summary>
    /// An attribute has no length of its own, and the refusal is one line.
    /// </summary>
    [Fact]
    public void CutAnUnboundedSpellingShort()
    {
        var refusal = PrivateBlockMarkup.DescribeSurfaceRefusal($"[private=\"{new string('я', 500)}\"]тайна[/private]");

        refusal.Length.Should().BeLessThan(400);
        refusal.Should().Contain("...");
    }

    [Theory]
    // Opened and never closed.
    [InlineData("до [private=Вася]без закрытия", "[private=Вася]", "символ 4")]
    // Closed without being opened.
    [InlineData("текст [/private] дальше", "[/private]", "символ 7")]
    // Opened inside itself.
    [InlineData("[private]раз [private=Петя]два[/private]", "[private=Петя]", "символ 14")]
    // Written after a verbatim block nobody closed.
    [InlineData("[noparse][private=\"Чак\"]СЕКРЕТ[/private]", "[private=\"Чак\"]", "символ 10")]
    public void TellTheAuthorWhichSpellingBrokeTheBlockAndWhere(
        string source, string spelling, string place)
    {
        var refusal = PrivateBlockMarkup.DescribeBalanceRefusal(source);

        refusal.Should().Contain(spelling);
        refusal.Should().Contain(place);
    }

    /// <summary>
    /// The verbatim block the author left open is named, because closing it is
    /// the fix and there are two names it could have.
    /// </summary>
    [Theory]
    [InlineData("[noparse][private=\"Чак\"]СЕКРЕТ[/private]", "[/noparse]")]
    [InlineData("[code][private=\"Чак\"]СЕКРЕТ[/private]", "[/code]")]
    public void NameTheVerbatimBlockThatWasLeftOpen(string source, string closing)
    {
        PrivateBlockMarkup.DescribeBalanceRefusal(source).Should().Contain(closing);
    }
}
