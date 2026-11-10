using AwesomeAssertions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// A block of the reading ends its line, so the one line break typed straight
/// after its closing tag is not drawn a second time. A hidden block is no block:
/// it is text in the line, and the breaks around it are drawn as typed.
/// </summary>
/// <remarks>
/// BBCODE_RENDERING.md, section "Перевод строки после блока"; the grammar table holds it on both sides,
/// these the cases it has no neutral way to state.
/// </remarks>
public class BbLineBreakAfterBlockShould
{
    private const string Opening =
        "<span class=\"private-message\" data-recipients=\"DungeonMaster, Assistant\">";

    private static readonly IAuthorizationSubject Author = BbVisibilityRender.Viewer(UserRole.RegularUser, BbVisibilityRender.AuthorId);

    private static readonly IAuthorizationSubject Stranger = BbVisibilityRender.Viewer(UserRole.RegularUser, BbVisibilityRender.OtherUser);

    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    private string Html(BbSurface surface, string bbCode, IAuthorizationSubject viewer) =>
        _parserProvider.GetForSurface(surface).RenderHtml(bbCode, new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = surface,
            Viewer = viewer,
            PostAuthorUserId = Author.UserId
        });

    private string Post(string bbCode) => Html(BbSurface.GamePost, bbCode, Author);

    private string Comment(string bbCode) => Html(BbSurface.Comment, bbCode, Author);

    [Fact]
    public void KeepTheBreakAfterAHiddenBlockWhichStandsInTheLine() =>
        Post("перед\n[private]секрет[/private]\nпосле").Should().Be(
            "перед<br />" + Opening + "секрет</span><br />после");

    [Fact]
    public void TakeALineBreakTypedAsCrLfTheSameWay() =>
        Post("перед\r\n[private]секрет[/private]\r\nпосле").Should().Be(
            Post("перед\n[private]секрет[/private]\nпосле"));

    // A CR alone is no line break on the page (BbParserProvider draws only CR LF and LF), so it is
    // no break to leave out either.
    [Fact]
    public void KeepACarriageReturnAlone() =>
        Comment("[quote]раз[/quote]\rдва").Should().Be("<div class=\"quote\">раз</div>\rдва");

    [Fact]
    public void DrawTheSecondLineBreakTheEmptyLineTheAuthorAskedFor() =>
        Comment("[quote]раз[/quote]\n\nпосле").Should().Be("<div class=\"quote\">раз</div><br />после");

    [Fact]
    public void KeepTheBreakAtTheEndOfTextWrittenAfterTheBlock() =>
        Post("[private]секрет[/private] и замечают.\nДверь").Should().Be(
            Opening + "секрет</span> и замечают.<br />Дверь");

    [Fact]
    public void DropTheLineTheBlockStoodOnForAReaderTheBlockIsHiddenFrom() =>
        Html(BbSurface.GamePost, "перед\n[private]секрет[/private]\nпосле", Stranger)
            .Should().Be("перед<br />после");

    [Theory]
    [InlineData(BbSurface.Comment, "[quote]раз[/quote]\nдва", "<div class=\"quote\">раз</div>два")]
    [InlineData(BbSurface.Comment, "[mod]раз[/mod]\nдва", "<div class=\"mod-block\">раз</div>два")]
    [InlineData(BbSurface.Comment, "[ul][li]раз[/li][/ul]\nдва", "<ul><li>раз</li></ul>два")]
    [InlineData(BbSurface.Comment, "[ol][li]раз[/li][/ol]\nдва", "<ol><li>раз</li></ol>два")]
    [InlineData(BbSurface.Comment, "[ul][/ul]\nдва", "<ul></ul>два")]
    [InlineData(BbSurface.Comment, "[li]раз[/li]\nдва", "<li>раз</li>два")]
    [InlineData(BbSurface.Comment, "[spoiler]раз[/spoiler]\nдва",
        "<a href=\"#\" class=\"spoiler-head\" data-swaptext=\"Скрыть содержимое\">Показать содержимое</a>" +
        "<div class=\"spoiler\">раз</div>два")]
    [InlineData(BbSurface.Comment, "[nsfw]раз[/nsfw]\nдва",
        "<a href=\"#\" class=\"nsfw-head\" data-swaptext=\"Скрыть шокирующий контент\">Показать шокирующий контент</a>" +
        "<div class=\"nsfw-spoiler\">раз</div>два")]
    [InlineData(BbSurface.GlobalChatMessage, "[quote]раз[/quote]\nдва", "<div class=\"quote\">раз</div>два")]
    [InlineData(BbSurface.Profile, "[quote]раз[/quote]\nдва", "<div class=\"quote\">раз</div>два")]
    [InlineData(BbSurface.DirectMessage, "[quote]раз[/quote]\nдва", "<div class=\"quote\">раз</div>два")]
    public void LeaveOutTheBreakAfterEveryBlockOfTheReading(BbSurface surface, string bbCode, string html) =>
        Html(surface, bbCode, Author).Should().Be(html);

    [Fact]
    public void LeaveItOutAfterABlockInsideABlock() =>
        Comment("[spoiler][quote]раз[/quote]\nдва[/spoiler]\nтри").Should().EndWith(
            "<div class=\"spoiler\"><div class=\"quote\">раз</div>два</div>три");

    [Theory]
    [InlineData("[b]раз[/b]\nдва", "<strong>раз</strong><br />два")]
    [InlineData("[code]раз[/code]\nдва", "<code class=\"code\">раз</code><br />два")]
    [InlineData("[link]https://example.com/[/link]\nдва",
        "<a href=\"https://example.com/\" target=\"_blank\" rel=\"noopener\">ссылка</a><br />два")]
    public void KeepTheBreakAfterATagThatStandsInTheLine(string bbCode, string html) =>
        Comment(bbCode).Should().Be(html);

    [Fact]
    public void KeepTheBreakBehindAnotherTag() =>
        Comment("[quote]раз[/quote][b]\nдва[/b]").Should().Be(
            "<div class=\"quote\">раз</div><strong><br />два</strong>");

    [Fact]
    public void KeepTheBreakAfterABlockThatIsText() =>
        Comment("[quote=\"Вася\"]раз[/quote]\nдва").Should().Be("[quote=\"Вася\"]раз[/quote]<br />два");

    [Fact]
    public void KeepTheBreakInThePlainText() =>
        _parserProvider.GetForSurface(BbSurface.Comment)
        .RenderText("[quote]раз[/quote]\nдва", RenderContext.ForPlainText() with { Surface = BbSurface.Comment })
        .Should().Be("раз\nдва");
}
