using System;
using AwesomeAssertions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Testing.Dsl;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// A reader a hidden block is erased for gets the text as if the block had not
/// been written, and its author and readers get what they always got.
/// </summary>
/// <remarks>
/// The erasure left marks of its own. A bold tag around the block stayed on the
/// page as an empty element, a link with the block inside it was printed as the
/// tags that were typed, "[link]", its address and "[/link]", and either told
/// the reader that something stood there. The rule is BBCODE_RENDERING.md,
/// section "Zero-information erase"; BbMarkupPropertiesShould holds the tags
/// around a block and the content of a block on generated texts, and these hold
/// the cases a generated text reaches too rarely: a block inside the one value
/// of a content tag, and the texts the owner looked at.
/// </remarks>
public class BbErasedBlockShould
{
    private static readonly Guid AuthorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid LeadId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid StrangerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private const string Opening =
        "<span class=\"private-message\" data-recipients=\"DungeonMaster, Assistant\">";

    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    private string Post(string bbCode, Guid viewer) =>
        ((BbParserWrapper)_parserProvider.GetForSurface(BbSurface.GamePost)).RenderHtml(bbCode, new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = BbSurface.GamePost,
            Viewer = new TestSubject
            {
                UserId = viewer,
                Role = UserRole.RegularUser,
                IsAuthenticated = true,
                AccessPolicy = AccessPolicy.NotSpecified
            },
            PostAuthorUserId = AuthorId,
            GameLeadUserIds = [LeadId]
        });

    private string PlainText(string bbCode) =>
        ((BbParserWrapper)_parserProvider.GetForSurface(BbSurface.GamePost))
        .RenderText(bbCode, RenderContext.ForPlainText() with { Surface = BbSurface.GamePost });

    /// <summary>
    /// The link the rest of its content makes: its address, and its own text or
    /// the word "ссылка".
    /// </summary>
    [Theory]
    [InlineData("[link]https://example.com/[private]x[/private][/link]",
        "<a href=\"https://example.com/\" target=\"_blank\" rel=\"noopener\">ссылка</a>")]
    [InlineData("[link]https://exa[private]x[/private]mple.com/[/link]",
        "<a href=\"https://example.com/\" target=\"_blank\" rel=\"noopener\">ссылка</a>")]
    [InlineData("[link text=\"Карта\"]https://example.com/[private]x[/private][/link]",
        "<a href=\"https://example.com/\" target=\"_blank\" rel=\"noopener\">Карта</a>")]
    [InlineData("[link]https://example.com/[b][private]x[/private][/b][/link]",
        "<a href=\"https://example.com/\" target=\"_blank\" rel=\"noopener\">ссылка</a>")]
    [InlineData("[mention]Вася[private]x[/private][/mention]",
        "<a class=\"bb-mention\" href=\"/users/%D0%92%D0%B0%D1%81%D1%8F\" data-bb-tag=\"mention\" " +
        "data-bb-user=\"Вася\">@Вася</a>")]
    public void GiveTheReaderTheLinkTheRestOfItsContentMakes(string bbCode, string html) =>
        Post(bbCode, StrangerId).Should().Be(html);

    [Fact]
    public void GiveTheReaderTheImageTheRestOfItsContentMakes() =>
        Post("[img]https://example.com/a.png[private]x[/private][/img]", StrangerId)
            .Should().StartWith("<img src=\"https://example.com/a.png\" class=\"bb-image\"");

    /// <summary>
    /// A link that held nothing but the block is left out with it, as a bold tag
    /// is: an empty address makes no link, and its tags printed as text would
    /// say what stood between them.
    /// </summary>
    [Theory]
    [InlineData("до [link][private]https://example.com/[/private][/link] после")]
    [InlineData("до [link] [private]https://example.com/[/private] [/link] после")]
    [InlineData("до [mention][private]Вася[/private][/mention] после")]
    public void LeaveOutALinkThatHeldNothingButTheBlock(string bbCode)
    {
        Post(bbCode, StrangerId).Should().Be("до  после");
        PlainText(bbCode).Should().Be("до  после");
    }

    /// <summary>
    /// The author and the readers of the block get the tag as typed, as before:
    /// a block inside a link is no address, and the save path does not refuse it.
    /// </summary>
    [Fact]
    public void GiveTheReaderOfTheBlockTheLinkAsTyped()
    {
        const string bbCode = "[link]https://example.com/[private]x[/private][/link]";
        const string asTyped = "[link]https://example.com/" + Opening + "x</span>[/link]";

        Post(bbCode, AuthorId).Should().Be(asTyped);
        Post(bbCode, LeadId).Should().Be(asTyped);
    }

    [Fact]
    public void GiveThePlainTextTheLinkTheRestOfItsContentMakes() =>
        PlainText("[link]https://example.com/[private]x[/private][/link]").Should().Be("https://example.com/");

    /// <summary>
    /// The post the owner looked at, line by line: a bold tag around the block,
    /// a link around it, and a dash on either side of it.
    /// </summary>
    /// <remarks>
    /// The hyphens on either side of the block are drawn as they were typed: the
    /// erased block does not join them into a dash, as a dash is what the author
    /// typed on neither side.
    /// </remarks>
    [Fact]
    public void ShowTheOwnersPost_AsIfTheBlocksWereNotThere()
    {
        const string bbCode = "Раз [b][private]x[/private][/b] два\n" +
                              "[link]https://example.com/[private]x[/private][/link]\n" +
                              "а-[private]x[/private]-б";

        Post(bbCode, StrangerId).Should().Be(
            "Раз  два<br />" +
            "<a href=\"https://example.com/\" target=\"_blank\" rel=\"noopener\">ссылка</a><br />" +
            "а--б");

        Post(bbCode, AuthorId).Should().Be(
            "Раз <strong>" + Opening + "x</span></strong> два<br />" +
            "[link]https://example.com/" + Opening + "x</span>[/link]<br />" +
            "а-" + Opening + "x</span>-б");
    }

    /// <summary>
    /// The example the owner gave for the look of hidden text: green words in
    /// the line, and who reads them after them, in the same line.
    /// </summary>
    [Fact]
    public void ShowTheHiddenTextInTheLine()
    {
        const string bbCode = "Эльвира обходит каюту и садится за стол капитана.\n" +
                              "[private]Она прячет найденный ключ под подушкой и никому о нем не говорит.[/private]\n" +
                              "Потом она выходит на палубу.";

        Post(bbCode, AuthorId).Should().Be(
            "Эльвира обходит каюту и садится за стол капитана.<br />" +
            Opening + "Она прячет найденный ключ под подушкой и никому о нем не говорит.</span><br />" +
            "Потом она выходит на палубу.");

        Post(bbCode, StrangerId).Should().Be(
            "Эльвира обходит каюту и садится за стол капитана.<br />Потом она выходит на палубу.",
            "the block stood on a line of its own, and an empty line in its place would say it had");
    }

    /// <summary>
    /// A line that held nothing but a block the reader does not read goes with the
    /// block and one of its line breaks: the reader gets the text as if the author had
    /// never typed that line.
    /// </summary>
    /// <remarks>
    /// The whole text begins and ends lines, so a block at its very start or end next to
    /// a break is on a line of its own too. Inside a tag only a line between two breaks
    /// is known to be one: the tag itself may stand in the middle of a line of text. A
    /// line with anything else on it stays as the author typed it, a space included,
    /// because the space is on that line without the block as well.
    /// </remarks>
    [Theory]
    [InlineData("раз\n[private]x[/private]\nдва", "раз<br />два")]
    [InlineData("раз\r\n[private]x[/private]\r\nдва", "раз<br />два")]
    [InlineData("[private]x[/private]\nдва", "два")]
    [InlineData("раз\n[private]x[/private]", "раз")]
    [InlineData("[private]x[/private]", "")]
    [InlineData("\n\n[private]x[/private]", "")]
    [InlineData("  [private]x[/private]  ", "")]
    [InlineData("раз\n[private]x[/private]\n[private]y[/private]\nдва", "раз<br />два")]
    [InlineData("[private]x[/private]\n[private]y[/private]\nдва", "два")]
    [InlineData("раз\n[private]x[/private]\n[private]y[/private]", "раз")]
    [InlineData("раз\n[private]x[/private][private]y[/private]\nдва", "раз<br />два")]
    [InlineData("раз\n[b][private]x[/private][/b]\nдва", "раз<br />два")]
    [InlineData("раз\n\n[private]x[/private]\nдва", "раз<br /><br />два")]
    [InlineData("[quote]раз\n[private]x[/private]\nдва[/quote]", "<div class=\"quote\">раз<br />два</div>")]
    [InlineData("[quote]цитата[/quote]\n[private]x[/private]\nдва", "<div class=\"quote\">цитата</div>два")]
    public void DropTheLineABlockStoodAloneOn(string bbCode, string html)
    {
        Post(bbCode, StrangerId).Should().Be(html);
    }

    /// <summary>
    /// A line that holds anything but erased blocks is the author's line and stays,
    /// and inside a tag the first and last lines are bounded by the tag, not by breaks.
    /// </summary>
    [Theory]
    [InlineData("раз\n [private]x[/private]\nдва", "раз<br /> <br />два")]
    [InlineData("раз\nи [private]x[/private]\nдва", "раз<br />и <br />два")]
    [InlineData("раз [b][private]x[/private]\nдва[/b]", "раз <strong><br />два</strong>")]
    public void KeepALineThatHoldsAnythingElse(string bbCode, string html)
    {
        Post(bbCode, StrangerId).Should().Be(html);
    }

    /// <summary>The author and the readers of the block keep every line, as they always did.</summary>
    [Fact]
    public void KeepTheLineForTheReadersOfTheBlock()
    {
        Post("раз\n[private]x[/private]\nдва", LeadId).Should().Be(
            "раз<br />" + Opening + "x</span><br />два");
    }

    /// <summary>The plain text of the mail and the search drops the line the same way.</summary>
    [Fact]
    public void DropTheLineFromThePlainTextToo()
    {
        PlainText("раз\n[private]x[/private]\nдва").Should().Be("раз\nдва");
    }
}
