using DM.Domain.Core.Content;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests;

/// <summary>
/// Hiding markup is refused where it cannot hide, and the refusal says enough
/// to act on.
/// </summary>
/// <remarks>
/// Only the game post declares [private], elsewhere the page erases the block for every reader.
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
    // [code] changes the font and shows nothing as an example: what it holds
    // is read as markup, and hides on the game post alone.
    [InlineData("[code][private=\"Чак\"]СЕКРЕТ[/private][/code]")]
    [InlineData("[code][/private][/code]")]
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
    [InlineData("[noparse][/private][/noparse]")]
    [InlineData("[code][noparse][private=\"Чак\"]пример[/private][/noparse][/code]")]
    public void PassTextThatHidesNothing(string? source)
    {
        PrivateBlockMarkup.ContainsPrivateMarkup(source).Should().BeFalse();
    }

    /// <summary>
    /// The refusal on a surface that does not declare the tag names three
    /// things: the spelling, where the tag works and how to show it as an
    /// example. No character number: the editor's check points at the line.
    /// </summary>
    [Fact]
    public void TellTheAuthorWhatWasReadAndWhereItWorks()
    {
        var refusal = PrivateBlockMarkup.DescribeSurfaceRefusal("до [private=\"Чак\"]СЕКРЕТ[/private] после");

        refusal.Should().Be(
            "Скрытый блок работает только в игровых постах: [private=\"Чак\"]. Для примера оберните его в [noparse].");
    }

    /// <summary>
    /// The refusal names where the tag works and promises nothing about who
    /// would read the block.
    /// </summary>
    [Fact]
    public void NotTellTheAuthorThatEveryReaderWouldSeeTheBlock()
    {
        var refusal = PrivateBlockMarkup.DescribeSurfaceRefusal("до [private=\"Чак\"]СЕКРЕТ[/private] после");

        refusal.Should().NotContain("увидят");
        refusal.Should().NotContain("читател");
    }

    /// <summary>
    /// With nothing to point at, the refusal is the reason alone, one sentence
    /// and so without a period.
    /// </summary>
    [Fact]
    public void GiveTheReasonAloneWhenThereIsNothingToPointAt()
    {
        PrivateBlockMarkup.DescribeSurfaceRefusal("без разметки")
            .Should().Be("Скрытый блок работает только в игровых постах");
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
    [InlineData("до [private=\"Вася\"]без закрытия", "[private=\"Вася\"]", "Скрытый блок не закрыт: [private=\"Вася\"]. Добавьте [/private].")]
    // Closed without being opened.
    [InlineData("текст [/private] дальше", "[/private]", "[/private] ничего не закрывает, уберите его или откройте [private] выше.")]
    // Opened inside itself.
    [InlineData("[private]раз [private=\"Петя\"]два[/private]", "[private=\"Петя\"]", "Скрытый блок внутри скрытого не работает: [private=\"Петя\"]. Закройте первый тегом [/private].")]
    // Written after a verbatim block nobody closed.
    [InlineData("[noparse][private=\"Чак\"]СЕКРЕТ[/private]", "[private=\"Чак\"]", "[private=\"Чак\"] не станет примером: [noparse] выше не закрыт тегом [/noparse].")]
    public void TellTheAuthorWhichSpellingBrokeTheBlockAndWhere(
        string source, string spelling, string expected)
    {
        var refusal = PrivateBlockMarkup.DescribeBalanceRefusal(source);

        refusal.Should().Contain(spelling);
        refusal.Should().Be(expected);
    }

    /// <summary>
    /// The [noparse] the author left open is named, because closing it is the
    /// fix. A [code] left open is no forgotten example, and the block after it
    /// is accepted.
    /// </summary>
    [Theory]
    [InlineData("[noparse][private=\"Чак\"]СЕКРЕТ[/private]")]
    [InlineData("[NoParse] и [/private]")]
    public void NameTheVerbatimBlockThatWasLeftOpen(string source)
    {
        PrivateBlockMarkup.DescribeBalanceRefusal(source)
            .Should().Contain("не станет примером: [noparse] выше не закрыт тегом [/noparse].");
    }
}
