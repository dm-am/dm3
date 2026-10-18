using System;
using AwesomeAssertions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Testing.Dsl;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// A block of the reading ends its line, so the one line break typed straight
/// after its closing tag is not drawn a second time. A hidden block is no block:
/// it is text in the line, and the breaks around it are drawn as typed.
/// </summary>
/// <remarks>
/// The element of a block ends the line it stands on, and a &lt;br /&gt; after it
/// stood alone on a line of its own: every [quote] written on its own line had
/// an empty line under it, and none over it, where the browser draws no line
/// for a break that ends the text in front of a block. The rule is
/// BBCODE_RENDERING.md, section "Перевод строки после блока"; the rows of the
/// grammar table hold it on both sides, and these hold the cases the table has
/// no neutral way to state.
/// </remarks>
public class BbLineBreakAfterBlockShould
{
    private const string Opening =
        "<span class=\"private-message\" data-recipients=\"DungeonMaster, Assistant\">";

    private static readonly TestSubject Author = new()
    {
        UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Role = UserRole.RegularUser,
        IsAuthenticated = true,
        AccessPolicy = AccessPolicy.NotSpecified
    };

    private static readonly TestSubject Stranger = new()
    {
        UserId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
        Role = UserRole.RegularUser,
        IsAuthenticated = true,
        AccessPolicy = AccessPolicy.NotSpecified
    };

    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    private string Html(BbSurface surface, string bbCode, TestSubject viewer) =>
        ((BbParserWrapper)_parserProvider.GetForSurface(surface)).RenderHtml(bbCode, new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = surface,
            Viewer = viewer,
            PostAuthorUserId = Author.UserId
        });

    private string Post(string bbCode) => Html(BbSurface.GamePost, bbCode, Author);

    private string Comment(string bbCode) => Html(BbSurface.Comment, bbCode, Author);

    /// <summary>
    /// A hidden block is text in the line, green and followed by who reads it,
    /// and ends no line: the break typed after it ends its line, as after text.
    /// </summary>
    [Fact]
    public void KeepTheBreakAfterAHiddenBlock_WhichStandsInTheLine() =>
        Post("перед\n[private]секрет[/private]\nпосле").Should().Be(
            "перед<br />" + Opening + "секрет</span><br />после");

    [Fact]
    public void TakeALineBreakTypedAsCrLfTheSameWay() =>
        Post("перед\r\n[private]секрет[/private]\r\nпосле").Should().Be(
            Post("перед\n[private]секрет[/private]\nпосле"));

    /// <summary>
    /// A CR alone is no line break on the page (BbParserProvider draws only CR LF
    /// and LF), so it is no break to leave out either.
    /// </summary>
    [Fact]
    public void KeepACarriageReturnAlone() =>
        Comment("[quote]раз[/quote]\rдва").Should().Be("<div class=\"quote\">раз</div>\rдва");

    [Fact]
    public void DrawTheSecondLineBreak_TheEmptyLineTheAuthorAskedFor() =>
        Comment("[quote]раз[/quote]\n\nпосле").Should().Be("<div class=\"quote\">раз</div><br />после");

    /// <summary>
    /// The break at the end of a sentence written after the block, on its line,
    /// ends that sentence. A rule in the styles that hid the break after the
    /// line of recipients took this one as well, and glued the sentence to the
    /// next line.
    /// </summary>
    [Fact]
    public void KeepTheBreakAtTheEndOfTextWrittenAfterTheBlock() =>
        Post("[private]секрет[/private] и замечают.\nДверь").Should().Be(
            Opening + "секрет</span> и замечают.<br />Дверь");

    /// <summary>
    /// A reader who does not see the block gets the text as if its line had not
    /// been typed: the block stood on a line of its own, and the line goes with it
    /// and with one of its breaks, where an empty line would say something stood
    /// there (BbErasedBlockShould holds the rest of that rule).
    /// </summary>
    [Fact]
    public void DropTheLineTheBlockStoodOn_ForAReaderTheBlockIsHiddenFrom() =>
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

    /// <summary>
    /// Only a break straight after the closing tag belongs to the block: one
    /// behind another tag is that tag's content.
    /// </summary>
    [Fact]
    public void KeepTheBreakBehindAnotherTag() =>
        Comment("[quote]раз[/quote][b]\nдва[/b]").Should().Be(
            "<div class=\"quote\">раз</div><strong><br />два</strong>");

    /// <summary>
    /// A block the grammar did not accept is text, and ends no line.
    /// </summary>
    [Fact]
    public void KeepTheBreakAfterABlockThatIsText() =>
        Comment("[quote=\"Вася\"]раз[/quote]\nдва").Should().Be("[quote=\"Вася\"]раз[/quote]<br />два");

    [Fact]
    public void GiveTheAuthorsEditorEveryBreakBack()
    {
        var html = ((BbParserWrapper)_parserProvider.GetForAuthorEdit(BbSurface.GamePost)).RenderHtml(
            "[private]секрет[/private]\nпосле\n[quote]раз[/quote]\nдва\n[spoiler]три[/spoiler]\nчетыре",
            RenderContext.ForAuthorEdit(Author, BbSurface.GamePost));

        html.Should().Contain("секрет</span><br />после");
        html.Should().Contain("раз</div><br />два");
        html.Should().Contain("три</div><br />четыре");
    }

    [Fact]
    public void KeepTheBreakInThePlainText() =>
        ((BbParserWrapper)_parserProvider.GetForSurface(BbSurface.Comment))
        .RenderText("[quote]раз[/quote]\nдва", RenderContext.ForPlainText() with { Surface = BbSurface.Comment })
        .Should().Be("раз\nдва");
}
