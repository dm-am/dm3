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

    /// <summary>
    /// Every address here names the machine the post is being read on, or the
    /// network around it, and the request would be issued by the reader's browser.
    /// Four spellings of 127.0.0.1 are listed on purpose: `new Uri` normalises
    /// three of them and leaves the IPv4-mapped one alone, which is precisely
    /// where comparing the host as a string stopped working. The IPv6 rows are
    /// here for the same reason — fd12:: and fe81:: are inside the blocked ranges
    /// while matching none of their common prefixes.
    /// </summary>
    [Theory]
    [InlineData("http://127.0.0.1/x.png")]
    [InlineData("http://127.1/x.png")]
    [InlineData("http://2130706433/x.png")]
    [InlineData("http://[::ffff:127.0.0.1]/x.png")]
    [InlineData("http://[::1]/x.png")]
    [InlineData("http://[fd12:3456::1]/x.png")]
    [InlineData("http://[fe81::1]/x.png")]
    [InlineData("http://[::ffff:192.168.0.1]/x.png")]
    [InlineData("http://[::]/x.png")]
    [InlineData("http://10.0.0.1/x.png")]
    [InlineData("http://172.20.0.1/x.png")]
    [InlineData("http://192.168.0.1/x.png")]
    [InlineData("http://169.254.169.254/x.png")]
    [InlineData("http://100.64.0.1/x.png")]
    [InlineData("http://0.0.0.0/x.png")]
    [InlineData("http://localhost/x.png")]
    [InlineData("http://LOCALHOST/x.png")]
    [InlineData("http://api.localhost/x.png")]
    public void DropImage_WhenUrlPointsIntoReaderNetwork(string url)
    {
        var tree = _parserProvider.CurrentCommon.Parse($"[img]{url}[/img]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().NotContain("<img");
    }

    /// <summary>
    /// The same guard on the [link] path, where a rejected URL costs the anchor
    /// but keeps its text.
    /// </summary>
    [Theory]
    [InlineData("http://[::ffff:127.0.0.1]/probe")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    public void DropAnchor_WhenLinkPointsIntoReaderNetwork(string url)
    {
        var tree = _parserProvider.CurrentCommon.Parse($"[link=tap]{url}[/link]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().NotContain("<a href");
        html.Should().Contain("tap");
    }

    /// <summary>
    /// The other half of the contract: a check that only ever says "no" would
    /// satisfy the theories above while breaking every image in the product. The
    /// last row is a host NAME that merely starts like a private address — the
    /// browser resolves it like any other name, and it was the string comparison,
    /// not the risk, that used to reject it.
    /// </summary>
    [Theory]
    [InlineData("https://example.com/x.png")]
    [InlineData("http://93.184.216.34/x.png")]
    [InlineData("http://[2001:db8::1]/x.png")]
    [InlineData("http://10.example.com/x.png")]
    public void KeepImage_WhenUrlIsPublic(string url)
    {
        var tree = _parserProvider.CurrentCommon.Parse($"[img]{url}[/img]");

        var html = ((BbParserWrapper.WrappedNodeTree)tree).ToHtml();

        html.Should().Contain($"<img src=\"{url}\"");
    }
}
