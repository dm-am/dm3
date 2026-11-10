using System;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// What a closing tag that does not match the open node closes, and what it
/// must never close.
/// </summary>
/// <remarks>
/// The security half: a stray closing tag must not move the rest of a [private] block out of it, which
/// neither the balance check, the normaliser nor the depth guard would catch.
/// </remarks>
public class BbMismatchedClosingTagShould
{
    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    private static readonly Guid Stranger = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Theory]
    // The stray closing tag stands inside the private block.
    [InlineData("[private=\"Вася\"]секрет[/b]хвост[/private]")]
    // The tag it names was opened before the block: the unwind would have to
    // cross the block boundary to reach it.
    [InlineData("[b]жирно [private=\"Вася\"]секрет[/b] и еще[/private]")]
    // Same, through a tag the reader never sees the inside of either way.
    [InlineData("[private=\"Вася\"]секрет[/quote]хвост[/private]")]
    [InlineData("[i]наклонно [private=\"Вася\"]секрет[/i] и еще[/private]")]
    public void KeepPrivateTextInsideWhenClosingTagDoesNotMatch(string input)
    {
        var html = RenderForStranger(input);

        html.Should().NotContain("секрет");
        html.Should().NotContain("хвост");
        html.Should().NotContain("и еще");
    }

    [Fact]
    public void KeepPrivateTextOutOfPlainTextWhenClosingTagDoesNotMatch()
    {
        var parser = _parserProvider.GetForSurface(BbSurface.GamePost);

        var text = parser.RenderText(
            "[private=\"Вася\"]секрет[/b]хвост[/private]",
            RenderContext.ForPlainText() with { Surface = BbSurface.GamePost });

        text.Should().NotContain("секрет");
        text.Should().NotContain("хвост");
    }

    /// <summary>
    /// The formatting half: an unwind stops at the nearest ancestor of that
    /// name instead of running to the root, so what was opened outside it stays
    /// open.
    /// </summary>
    [Fact]
    public void CloseOnlyUpToTheNearestMatchingAncestor()
    {
        var parser = _parserProvider.GetForSurface(BbSurface.Comment);

        var html = parser.RenderHtml(
            "[b]жирно [i]наклонно [spoiler]скрыто[/i] дальше[/b] конец",
            new RenderContext { Audience = RenderAudience.Display, Surface = BbSurface.Comment });

        // "дальше" is written after [/i] and before [/b], so it is bold and no longer italic.
        html.Should().Contain("дальше");
        html.Should().MatchRegex("<strong>.*дальше.*</strong>");
        html.Should().NotMatchRegex("<em>[^<]*дальше");
    }

    [Fact]
    public void CloseNothingWhenNoAncestorCarriesThatName()
    {
        var parser = _parserProvider.GetForSurface(BbSurface.Comment);

        var html = parser.RenderHtml(
            "[b]жирно [/i] дальше[/b] конец",
            new RenderContext { Audience = RenderAudience.Display, Surface = BbSurface.Comment });

        html.Should().MatchRegex("<strong>.*дальше.*</strong>");
        html.Should().Contain("конец");
    }

    private string RenderForStranger(string input)
    {
        var parser = _parserProvider.GetForSurface(BbSurface.GamePost);
        return parser.RenderHtml(input, new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = BbSurface.GamePost,
            Viewer = BbVisibilityRender.Viewer(UserRole.RegularUser, Stranger),
            PostAuthorUserId = BbVisibilityRender.AuthorId
        });
    }

}
