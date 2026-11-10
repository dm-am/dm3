using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Infrastructure.Core.Parsing;
using Xunit;
using static DM.Infrastructure.Core.Tests.BbAudienceRender;

namespace DM.Infrastructure.Core.Tests;

public class BbImageTagShould
{
    // Image markup is emitted by this renderer, the client renderer and the TipTap node; the client
    // half is bbcodePaste.spec.ts.
    [Theory]
    [InlineData("class=\"bb-image\"")]
    [InlineData("data-bb-tag=\"img\"")]
    [InlineData("loading=\"lazy\"")]
    [InlineData("decoding=\"async\"")]
    [InlineData("referrerpolicy=\"no-referrer\"")]
    public void EmitEveryAgreedImageAttribute(string attribute)
    {
        Comment("[img]https://example.com/image.png[/img]").Should().Contain(attribute);
    }

    /// <summary>
    /// The attribute spellings of the tag are no tag at all.
    /// </summary>
    /// <remarks>Neither DM2 nor the grammar has such a form, so it is a broken tag shown as typed.</remarks>
    [Theory]
    [InlineData("http://127.0.0.1/x.png")]
    [InlineData("http://169.254.169.254/x.png")]
    [InlineData("https://example.org/picture.png")]
    [InlineData("ftp://evil.example/x.png")]
    public void ShowTheAttributeFormAsText(string url)
    {
        Comment($"[img=\"{url}\"]").Should().Be($"[img=\"{url}\"]");
        Comment($"[img={url}]").Should().Be($"[img={url}]");
    }

    [Theory]
    [InlineData("[img width=\"200\"]https://example.org/p.png[/img]", "data-bb-width=\"200\"")]
    [InlineData("[img height=\"100\"]https://example.org/p.png[/img]", "data-bb-height=\"100\"")]
    [InlineData("[img width=\"200\" height=\"100\"]https://example.org/p.png[/img]",
        "style=\"--bb-image-max-width:200px;--bb-image-max-height:100px\"")]
    public void SizeTheImageByItsParameters(string bbCode, string expected)
    {
        var html = Comment(bbCode);

        html.Should().Contain("<img").And.Contain("example.org/p.png").And.Contain(expected);
    }

    [Theory]
    [InlineData("__IMG_0__")]
    [InlineData("I0")]
    public void RenderAnImageOnlyWhereItsTagStood(string typedByAuthor)
    {
        var html = Comment($"{typedByAuthor} [img]https://example.com/a.png[/img]");

        (html.Split("<img ").Length - 1).Should().Be(1);
    }

    /// <summary>
    /// The same holds where text can never become an element: the recipients a
    /// private block carries are built from a tag attribute.
    /// </summary>
    [Fact]
    public void KeepTheAttributeAsTextWhenItLooksLikeAnImage()
    {
        var html = Html(BbSurface.GamePost,
            "[private=\"__IMG_0__\"]text[/private][img]https://example.com/a.png[/img]");

        html.Should().Contain(
            "<span class=\"private-message\" data-recipients=\"DungeonMaster, Assistant, __IMG_0__\">text</span>");
    }

    // The global chat puts an image behind a spoiler marked as the gate, which the client opens in
    // the full layout and keeps closed in the compact one
    [Theory]
    [InlineData(BbSurface.GlobalChatMessage, true)]
    [InlineData(BbSurface.GamePost, false)]
    [InlineData(BbSurface.Comment, false)]
    [InlineData(BbSurface.Profile, false)]
    [InlineData(BbSurface.DirectMessage, false)]
    public void GateImagesOnlyWhereTheSurfaceAsksForIt(BbSurface surface, bool gated)
    {
        var html = Html(surface, "[img]https://example.com/a.png[/img]");

        html.Should().Contain("<img ");
        html.Contains("spoiler-head").Should().Be(gated);
        html.Contains("data-bb-gate=\"image\"").Should().Be(gated);
    }

    // The gate the author wrote is not the reader's to open: a spoiler around an image in the chat
    // carries no mark, and the image inside it carries its own.
    [Fact]
    public void MarkOnlyTheGateTheServerAdded()
    {
        var html = Html(BbSurface.GlobalChatMessage, "[spoiler][img]https://example.com/a.png[/img][/spoiler]");

        html.Should().StartWith(
            "<a href=\"#\" class=\"spoiler-head\" data-swaptext=\"Скрыть содержимое\">Показать содержимое</a>" +
            "<div class=\"spoiler\"><a href=\"#\" class=\"spoiler-head\" data-bb-gate=\"image\" ");
    }

    /// <summary>
    /// The spoiler an image is put behind is a spoiler without a caption: its head
    /// says "Скрыть содержимое" once open, as the head of one the author wrote does.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SwapTheWordsOfTheGateAsASpoilerDoes(bool safe)
    {
        const string text = "[img]https://example.com/a.png[/img]";
        var gated = safe ? EmbedSafe(BbSurface.Comment, text) : Html(BbSurface.GlobalChatMessage, text);
        var spoiler = Html(BbSurface.Comment, "[spoiler]x[/spoiler]");

        var head = Regex.Match(gated, "<a [^>]*class=\"spoiler-head\"[^>]*>[^<]*</a>").Value
            .Replace(" data-bb-gate=\"image\"", "");
        head.Should().NotBeEmpty();
        spoiler.Should().StartWith(head);
    }

    /// <summary>
    /// And the audience that exists to be embeddable safely gets it everywhere,
    /// with no mark: it has no layout to ask.
    /// </summary>
    [Theory]
    [InlineData(BbSurface.GamePost)]
    [InlineData(BbSurface.Comment)]
    [InlineData(BbSurface.Profile)]
    public void GateImagesOnEverySafeParser(BbSurface surface)
    {
        var html = EmbedSafe(surface, "[img]https://example.com/a.png[/img]");

        html.Should().Contain("<img ").And.Contain("spoiler-head").And.NotContain("data-bb-gate");
    }

    /// <summary>
    /// An image tag the author never closed costs them the tag, and nothing else.
    /// </summary>
    /// <remarks>The content pattern stops at the next [img], so an unclosed tag does not swallow the next image.</remarks>
    [Fact]
    public void EatNothingBetweenAnUnclosedImageAndTheNextClosedOne()
    {
        var html = Comment(
            "начало [img]https://example.com/a.png\nсередина\n[img]https://example.com/b.png[/img] конец");

        html.Should().Contain("начало").And.Contain("середина").And.Contain("конец");
        // The tag that was never closed stays where it stands, as the text it is,
        // and the one that was closed still renders.
        html.Should().Contain("[img]https://example.com/a.png");
        html.Should().Contain("<img src=\"https://example.com/b.png\"");
    }
}
