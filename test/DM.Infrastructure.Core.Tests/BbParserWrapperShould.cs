using DM.Infrastructure.Core.Parsing;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

public class BbParserWrapperShould
{
    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    [Fact]
    public void PreserveImgAndLinkTags_ToBb()
    {
        var input = "[img]https://example.com/image.png[/img]\n\n[link]https://example.com[/link]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var wrapped = tree as BbParserWrapper.WrappedNodeTree;
        wrapped.Should().NotBeNull("Parse should return WrappedNodeTree");

        var result = wrapped!.ToBb();
        result.Should().Contain("[img]https://example.com/image.png[/img]");
        result.Should().Contain("[link]https://example.com[/link]");
    }

    /// <summary>
    /// Image markup is emitted from three places — this renderer, the client
    /// renderer and the TipTap node — and they have drifted before: one grew a
    /// data-alt the others never had. This is the server half of the guard; the
    /// client half lives in bbcode.spec.ts. The attribute SET is frozen, not the
    /// byte order: the three legitimately order attributes differently, so a byte
    /// comparison would fail for the wrong reason.
    /// </summary>
    [Theory]
    [InlineData("class=\"bb-image\"")]
    [InlineData("data-bb-tag=\"img\"")]
    [InlineData("loading=\"lazy\"")]
    [InlineData("decoding=\"async\"")]
    [InlineData("referrerpolicy=\"no-referrer\"")]
    public void EmitEveryAgreedImageAttribute(string attribute)
    {
        var tree = _parserProvider.CurrentCommon
            .Parse("[img]https://example.com/image.png[/img]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().Contain(attribute);
    }

    [Fact]
    public void PreserveImgAndLinkTags_ToHtml()
    {
        var input = "[img]https://example.com/image.png[/img]\n\n[link]https://example.com[/link]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var wrapped = tree as BbParserWrapper.WrappedNodeTree;
        wrapped.Should().NotBeNull("Parse should return WrappedNodeTree");

        var result = wrapped!.ToHtml();
        result.Should().Contain("<img src=\"https://example.com/image.png\"");
        result.Should().Contain("<a href=\"https://example.com\"");
    }

    [Fact]
    public void PreserveLinkWithText_ToBb()
    {
        var input = "[link=Click here]https://example.com[/link]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var wrapped = tree as BbParserWrapper.WrappedNodeTree;
        var result = wrapped!.ToBb();
        result.Should().Be("[link=Click here]https://example.com[/link]");
    }

    [Fact]
    public void PreserveLinkWithText_ToHtml()
    {
        var input = "[link=Click here]https://example.com[/link]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var wrapped = tree as BbParserWrapper.WrappedNodeTree;
        var result = wrapped!.ToHtml();
        result.Should().Contain("<a href=\"https://example.com\"");
        result.Should().Contain(">Click here</a>");
    }

    [Fact]
    public void PreserveMixedContent()
    {
        var input = "[b]Bold text[/b] with [img]https://img.com/a.png[/img] and [link]https://link.com[/link]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var wrapped = tree as BbParserWrapper.WrappedNodeTree;
        var result = wrapped!.ToBb();

        result.Should().Contain("[b]Bold text[/b]");
        result.Should().Contain("[img]https://img.com/a.png[/img]");
        result.Should().Contain("[link]https://link.com[/link]");
    }

    [Fact]
    public void HandleMultipleImagesAndLinks()
    {
        var input = "[img]https://img1.com[/img][img]https://img2.com[/img][link]https://link1.com[/link][link=text]https://link2.com[/link]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var wrapped = tree as BbParserWrapper.WrappedNodeTree;
        var result = wrapped!.ToBb();

        result.Should().Contain("[img]https://img1.com[/img]");
        result.Should().Contain("[img]https://img2.com[/img]");
        result.Should().Contain("[link]https://link1.com[/link]");
        result.Should().Contain("[link=text]https://link2.com[/link]");
    }

    // NOTE: [spoiler=title] is NOT supported - only simple [spoiler] is handled by BBCodeParser

    [Fact]
    public void ConvertSimpleSpoiler_ToHtml_UsesDefaultText()
    {
        var input = "[spoiler]hidden content[/spoiler]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var wrapped = tree as BbParserWrapper.WrappedNodeTree;
        var result = wrapped!.ToHtml();

        result.Should().Contain("class=\"spoiler-head\"");
        result.Should().Contain($">{BbParserWrapper.DefaultSpoilerText}</a>");
        result.Should().Contain("hidden content");
    }

    /// <summary>
    /// A [link] whose URL is rejected by SanitizeUrl still has to render its
    /// display text, and that text is attacker-controlled: it must be HTML
    /// encoded exactly like the accepted-URL branch encodes it. Emitting it raw
    /// is stored XSS on every content surface (comments, posts, profiles, DMs).
    /// </summary>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHN2Zz4=")]
    [InlineData("vbscript:msgbox(1)")]
    public void EncodeLinkText_WhenUrlIsRejected(string dangerousUrl)
    {
        var input = $"[link=<img src=x onerror=alert(1)>]{dangerousUrl}[/link]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var result = ((BbParserWrapper.WrappedNodeTree)tree!).ToHtml();

        result.Should().NotContain("<img src=x onerror=alert(1)>");
        result.Should().Contain("&lt;img src=x onerror=alert(1)&gt;");
    }

    /// <summary>
    /// Same guarantee for the accepted-URL branch, so the two paths cannot
    /// drift apart again.
    /// </summary>
    [Fact]
    public void EncodeLinkText_WhenUrlIsAccepted()
    {
        var input = "[link=<b>bold</b>]https://example.com[/link]";
        var parser = _parserProvider.CurrentCommon;
        var tree = parser.Parse(input);

        var result = ((BbParserWrapper.WrappedNodeTree)tree!).ToHtml();

        result.Should().NotContain("<b>bold</b>");
        result.Should().Contain("&lt;b&gt;bold&lt;/b&gt;");
    }
}
