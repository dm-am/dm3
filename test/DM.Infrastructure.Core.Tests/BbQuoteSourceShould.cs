using System;
using System.Collections.Generic;
using BBCodeParser;
using BBCodeParser.Tags;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// What the source of a quotation carries, and what it never carries.
/// </summary>
/// <remarks>
/// One rule governs the whole file: a quotation source shows its reader no more
/// than the page would have shown the same reader. By the private block it is
/// stricter than the page - quoting is republication, and the addressees of the
/// original block do not travel with the text - and by everything else it is
/// equal, which is the half that has to be held on purpose rather than assumed.
/// </remarks>
public class BbQuoteSourceShould
{
    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    private static readonly Guid AuthorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LeadId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Stranger = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid GameId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    // ════════════════════════════════════════════════════════════════
    // The private block never travels
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void DropPrivateBlock_ForAStranger()
    {
        var source = QuoteGamePost(
            "до [private=\"Вася\"]секрет[/private] после",
            Viewer(Stranger));

        source.Source.Should().NotContain("секрет");
        source.Source.Should().Contain("до");
        source.Source.Should().Contain("после");
    }

    [Fact]
    public void DropPrivateBlock_ForThePostAuthorToo()
    {
        // The page shows the author his own private block; the quotation does
        // not. In a new post the same block would be read by a different set of
        // people, and nobody asked for that when they pressed "quote".
        var source = QuoteGamePost(
            "до [private=\"Вася\"]секрет[/private] после",
            Viewer(AuthorId));

        source.Source.Should().NotContain("секрет");
    }

    [Fact]
    public void DropPrivateBlock_ForAGameLeadToo()
    {
        var source = QuoteGamePost(
            "до [private=\"Вася\"]секрет[/private] после",
            Viewer(LeadId),
            leads: [LeadId]);

        source.Source.Should().NotContain("секрет");
    }

    [Fact]
    public void DropPrivateBlock_LeftOpenByAMismatchedClosingTag()
    {
        // The block reaches the end of the text because [/b] no longer ends it.
        // Neither half of it may be quoted.
        var source = QuoteGamePost(
            "до [private=\"Вася\"]секрет[/b]хвост[/private] после",
            Viewer(Stranger));

        source.Source.Should().NotContain("секрет");
        source.Source.Should().NotContain("хвост");
    }

    // ════════════════════════════════════════════════════════════════
    // The flag that tells the author what he lost
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void ReportStrippedPrivateText_ToAReaderWhoCanSeeIt()
    {
        var source = QuoteGamePost(
            "до [private=\"Вася\"]секрет[/private] после",
            Viewer(AuthorId));

        source.PrivateTextStripped.Should().BeTrue();
    }

    [Fact]
    public void NotReportStrippedPrivateText_ToAReaderWhoNeverSawIt()
    {
        // Otherwise the flag announces the existence of a block whose whole
        // point is that this reader does not know it is there.
        var source = QuoteGamePost(
            "до [private=\"Вася\"]секрет[/private] после",
            Viewer(Stranger));

        source.PrivateTextStripped.Should().BeFalse();
    }

    [Fact]
    public void NotReportStrippedPrivateText_WhenThereIsNone()
    {
        var source = QuoteGamePost("обычный текст", Viewer(AuthorId));

        source.PrivateTextStripped.Should().BeFalse();
    }

    // ════════════════════════════════════════════════════════════════
    // A quotation of a quotation
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void DropNestedQuotation_WithItsContent()
    {
        // Kept, the chain grows by one level on every reply. The nesting guard
        // in the parser stands against a page that cannot be rendered, not
        // against this.
        var source = QuoteComment("свое [quote=\"Петя\"]чужое[/quote] и еще свое");

        source.Source.Should().NotContain("чужое");
        source.Source.Should().NotContain("Петя");
        source.Source.Should().NotContain("[quote");
        source.Source.Should().Contain("свое");
        source.Source.Should().Contain("и еще свое");
    }

    // ════════════════════════════════════════════════════════════════
    // Everything else comes back as the author wrote it
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void KeepOrdinaryMarkup()
    {
        var source = QuoteComment("[b]жирно[/b] и [i]наклонно[/i]");

        source.Source.Should().Contain("[b]жирно[/b]");
        source.Source.Should().Contain("[i]наклонно[/i]");
    }

    [Fact]
    public void GiveAttributeValuesBackUnencoded()
    {
        // The parser reports the encoded value, because the wrapper encodes
        // every attribute it substitutes into markup. On the page the browser
        // decodes it back; in a composer there is no browser, so an author who
        // quoted this would save the entity text and see it on the page.
        //
        // Over a tag set of its own, because the two tags that carry an
        // attribute today - [quote] and [private] - are the two a quotation
        // drops, so nothing in the product's own sets can show the rule holding.
        // The rule is about the source path rather than about those two tags,
        // and the target grammar gives attributes to more of them.
        var withAttribute = new Tag("cite", "<div class=\"cite\">{value}</div>", "</div>", true, false);
        var parser = new BbParserWrapper(new BbParser(
            [withAttribute],
            BbParser.SecuritySubstitutions,
            new Dictionary<string, string>()));

        var source = parser.RenderQuoteSource("[cite=\"A & B\"]внутри[/cite]", new RenderContext
        {
            Audience = RenderAudience.QuoteSource,
            Surface = BbSurface.Comment,
            Viewer = Viewer(Stranger)
        });

        source.Source.Should().Contain("[cite=\"A & B\"]");
        source.Source.Should().NotContain("&amp;");
    }

    [Fact]
    public void KeepImageAndLinkMarkup()
    {
        var source = QuoteComment(
            "[img]https://example.com/a.png[/img] и [link=тут]https://example.com[/link]");

        source.Source.Should().Contain("[img]https://example.com/a.png[/img]");
        source.Source.Should().Contain("[link=тут]https://example.com[/link]");
    }

    [Fact]
    public void RefuseTheSameAddressesTheDisplayPathRefuses()
    {
        // The page drops an image on a dangerous scheme whole and leaves a link
        // on one as its bare text. A source that handed the address back would
        // be showing its reader something the page showed nobody.
        var source = QuoteComment(
            "[img]javascript:alert(1)[/img][link=текст]javascript:alert(1)[/link]");

        source.Source.Should().NotContain("javascript:");
        source.Source.Should().NotContain("[img");
        source.Source.Should().NotContain("[link");
        source.Source.Should().Contain("текст");
    }

    // ════════════════════════════════════════════════════════════════
    // The header
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void CarryTheAuthorName()
    {
        QuoteBlockMarkup.Compose("тело", "Вася").Should().StartWith("[quote=\"Вася\"]");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[[[")]
    public void OmitTheAttributeWithoutAName(string? authorName)
    {
        // No attribute means no header element when the block is drawn. That is
        // the ordinary case rather than a corner one: the DM2 tag has no
        // attribute at all, so the whole imported archive quotes this way.
        QuoteBlockMarkup.Compose("тело", authorName).Should().StartWith("[quote]");
    }

    [Theory]
    // The bracket is the one character no spelling of an attribute value can
    // carry: it is what starts the next tag.
    [InlineData("Вася[Пупкин", "ВасяПупкин")]
    // A quote survives, including one at the very end of the name.
    [InlineData("Джон \"Быстрый\" Смит", "Джон \"Быстрый\" Смит")]
    [InlineData("Вася\"", "Вася\"")]
    // A closing bracket survives too - the value class ends at the opening one.
    [InlineData("Ва]ся", "Ва]ся")]
    [InlineData("Вася\nПупкин", "Вася Пупкин")]
    public void CarryTheNameThroughTheGrammarUnchanged(string authorName, string expected)
    {
        var composed = QuoteBlockMarkup.Compose("тело", authorName);
        var parsed = _parserProvider.GetForSurface(BbSurface.Comment).Parse(composed);

        // What the parser reads back out of the tag it just built is the test:
        // a name that does not survive its own grammar is a quotation that
        // renders as its own literal text.
        var html = ((BbParserWrapper.WrappedNodeTree)parsed).ToHtml();
        html.Should().Contain("quote-author");
        html.Should().Contain(System.Web.HttpUtility.HtmlEncode(expected));
    }

    // ════════════════════════════════════════════════════════════════
    // Not something a client may ask for
    // ════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("quote_source")]
    [InlineData("quotesource")]
    [InlineData("quote")]
    [InlineData("source")]
    [InlineData("bb")]
    public void NotBeReachableThroughTheAudienceHeader(string spelling)
    {
        // The header applies to every BbText field of every response, and this
        // audience emits BBCode source - author text no security substitution
        // has run over. Reachable by header, it would put that text into fields
        // the client binds through v-html.
        BbAudienceHeader.Parse(spelling).Should().NotBe(RenderAudience.QuoteSource);
    }

    // ════════════════════════════════════════════════════════════════

    private QuoteSourceResult QuoteGamePost(
        string input, IAuthorizationSubject viewer, IReadOnlyCollection<Guid>? leads = null)
    {
        var parser = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.GamePost);
        return parser.RenderQuoteSource(input, new RenderContext
        {
            Audience = RenderAudience.QuoteSource,
            Surface = BbSurface.GamePost,
            Viewer = viewer,
            PostAuthorUserId = AuthorId,
            GameId = GameId,
            GameLeadUserIds = leads ?? Array.Empty<Guid>()
        });
    }

    private QuoteSourceResult QuoteComment(string input)
    {
        var parser = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.Comment);
        return parser.RenderQuoteSource(input, new RenderContext
        {
            Audience = RenderAudience.QuoteSource,
            Surface = BbSurface.Comment,
            Viewer = Viewer(Stranger)
        });
    }

    private static IAuthorizationSubject Viewer(Guid userId) =>
        new TestSubject
        {
            UserId = userId,
            Role = UserRole.RegularUser,
            IsAuthenticated = true,
            AccessPolicy = AccessPolicy.NotSpecified
        };

}
