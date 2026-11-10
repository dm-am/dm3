using AwesomeAssertions;
using DM.Infrastructure.Core.Parsing;
using Xunit;
using static DM.Infrastructure.Core.Tests.BbAudienceRender;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// The address an image or a link may point at (BbAddress): what is refused is
/// printed as typed, and what is public renders (BBCODE_RENDERING.md, "Правило
/// безопасного отказа").
/// </summary>
public class BbAddressShould
{
    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    // Four spellings of 127.0.0.1: `new Uri` normalises three and leaves the IPv4-mapped one alone;
    // fd12:: and fe81:: match no common prefix.
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
    public void PrintTheImageTagAsTypedWhenUrlPointsIntoReaderNetwork(string url)
    {
        Comment($"[img]{url}[/img]").Should().Be($"[img]{url}[/img]");
    }

    /// <summary>
    /// The same guard on the [link] path: a rejected URL costs the anchor, and
    /// the tag is printed as it was typed, so the author and the reader both see
    /// what did not work.
    /// </summary>
    [Theory]
    [InlineData("http://[::ffff:127.0.0.1]/probe")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    public void PrintTheLinkTagAsTypedWhenLinkPointsIntoReaderNetwork(string url)
    {
        var html = Comment($"[link text=\"tap\"]{url}[/link]");

        html.Should().NotContain("<a href");
        html.Should().Be($"[link text=\"tap\"]{url}[/link]");
    }

    /// <summary>
    /// The plain text of a refused image or link is the tag as typed as well:
    /// the text channels show what the page shows.
    /// </summary>
    [Theory]
    [InlineData("[img]javascript:alert(1)[/img]")]
    [InlineData("[link text=\"tap\"]http://127.0.0.1/[/link]")]
    public void PrintARefusedAddressAsTypedInThePlainTextToo(string bbCode)
    {
        var wrapper = _parserProvider.GetForSurface(BbSurface.Comment);

        wrapper.RenderText(bbCode, RenderContext.ForPlainText() with { Surface = BbSurface.Comment })
            .Should().Be(bbCode);
    }

    // A check that only says "no" would pass the theories above.
    [Theory]
    [InlineData("https://example.com/x.png")]
    [InlineData("http://93.184.216.34/x.png")]
    [InlineData("http://[2001:db8::1]/x.png")]
    [InlineData("http://10.example.com/x.png")]
    public void KeepImageWhenUrlIsPublic(string url)
    {
        Comment($"[img]{url}[/img]").Should().Contain($"<img src=\"{url}\"");
    }

    // Raw whitespace turns a quoted attribute into two, an event handler in the second row; such
    // content is not one address and shows as text.
    [Theory]
    [InlineData("https://example.com/a b.png")]
    [InlineData("https://example.com/a.png onmouseover=alert(1) x=")]
    [InlineData("https://example.com/a\tb.png")]
    [InlineData("https://example.com/a\nb.png")]
    public void DropImageWhenUrlCarriesWhitespace(string url)
    {
        Comment($"[img]{url}[/img]").Should().NotContain("<img");
    }

    /// <summary>
    /// And its counterweight: the newlines an editor leaves around a pasted
    /// address are still trimmed, so the rule above costs no working image.
    /// </summary>
    [Fact]
    public void KeepImageWhenUrlIsPaddedWithNewlines()
    {
        Comment("[img]\n  https://example.com/a.png\n[/img]").Should().Contain("<img src=\"https://example.com/a.png\"");
    }

    [Theory]
    [InlineData("http://[2001:db8::1]/x.png")]
    [InlineData("https://example.com/x.png?filter[name]=a")]
    public void KeepImageWhenTheUrlItselfCarriesBrackets(string url)
    {
        Comment($"[img]{url}[/img]").Should().Contain("<img src=");
    }
}
