using System;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// What a closing tag that does not match the open node closes, and what it
/// must never close.
/// </summary>
/// <remarks>
/// The rule has a security half and a formatting half, and the security half is
/// why the file exists. A closing tag whose name belongs to some ancestor used
/// to unwind the whole stack to the root, so everything written after it left
/// the block it was written inside - including a [private] block, whose
/// remainder was then printed to the room while the visibility filter dutifully
/// removed the part before the stray tag. Nothing else caught it: the balance
/// check at save time counts [private] tags and finds one open and one close,
/// the normaliser rewrites spellings rather than structure, and the depth guard
/// measures depth.
///
/// The input is not adversarial. Opening a formatting tag before a block and
/// closing it inside is the most ordinary nesting mistake there is.
/// </remarks>
public class BbMismatchedClosingTagShould
{
    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    private static readonly Guid AuthorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
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
    public void KeepPrivateTextInside_WhenClosingTagDoesNotMatch(string input)
    {
        var html = RenderForStranger(input);

        html.Should().NotContain("секрет");
        html.Should().NotContain("хвост");
        html.Should().NotContain("и еще");
    }

    [Fact]
    public void KeepPrivateTextOutOfPlainText_WhenClosingTagDoesNotMatch()
    {
        var parser = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.GamePost);

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
        var parser = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.Comment);

        var html = parser.RenderHtml(
            "[b]жирно [i]наклонно [spoiler]скрыто[/i] дальше[/b] конец",
            new RenderContext { Audience = RenderAudience.Display, Surface = BbSurface.Comment });

        // "дальше" is written after [/i] and before [/b], so it is bold and no
        // longer italic. Unwound to the root it used to be neither.
        html.Should().Contain("дальше");
        html.Should().MatchRegex("<strong>.*дальше.*</strong>");
        html.Should().NotMatchRegex("<em>[^<]*дальше");
    }

    /// <summary>
    /// A closing tag naming nothing that is open closes nothing, and in
    /// particular does not end a block it stands inside.
    /// </summary>
    [Fact]
    public void CloseNothing_WhenNoAncestorCarriesThatName()
    {
        var parser = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.Comment);

        var html = parser.RenderHtml(
            "[b]жирно [/i] дальше[/b] конец",
            new RenderContext { Audience = RenderAudience.Display, Surface = BbSurface.Comment });

        html.Should().MatchRegex("<strong>.*дальше.*</strong>");
        html.Should().Contain("конец");
    }

    private string RenderForStranger(string input)
    {
        var parser = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.GamePost);
        return parser.RenderHtml(input, new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = BbSurface.GamePost,
            Viewer = new TestSubject
            {
                UserId = Stranger,
                Role = UserRole.RegularUser,
                IsAuthenticated = true,
                AccessPolicy = AccessPolicy.NotSpecified
            },
            PostAuthorUserId = AuthorId
        });
    }

}
