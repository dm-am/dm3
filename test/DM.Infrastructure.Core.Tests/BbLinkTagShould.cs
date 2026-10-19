using AwesomeAssertions;
using Xunit;
using static DM.Infrastructure.Core.Tests.BbAudienceRender;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// The [link] tag: its word and its own text, encoded whether the address is
/// accepted or refused (BBCODE_RENDERING.md, "Набор тегов").
/// </summary>
public class BbLinkTagShould
{
    [Fact]
    public void PreserveImgAndLinkTagsToHtml()
    {
        var result = Comment("[img]https://example.com/image.png[/img]\n\n[link]https://example.com[/link]");

        result.Should().Contain("<img src=\"https://example.com/image.png\"");
        result.Should().Contain("<a href=\"https://example.com\"");
        result.Should().Contain(">ссылка</a>");
    }

    [Fact]
    public void ShowTheTextOfALinkWhenItWasGivenOne()
    {
        var result = Comment("[link text=\"Click here\"]https://example.com[/link]");

        result.Should().Be("<a href=\"https://example.com\" target=\"_blank\" rel=\"noopener\">Click here</a>");
    }

    /// <summary>
    /// A [link] whose URL is rejected is printed as typed, and what was typed is encoded like any other text.
    /// </summary>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHN2Zz4=")]
    [InlineData("vbscript:msgbox(1)")]
    public void EncodeLinkTextWhenUrlIsRejected(string dangerousUrl)
    {
        var result = Comment($"[link text=\"<img src=x onerror=alert(1)>\"]{dangerousUrl}[/link]");

        result.Should().NotContain("<img src=x onerror=alert(1)>");
        result.Should().Be($"[link text=\"&lt;img src=x onerror=alert(1)&gt;\"]{dangerousUrl}[/link]");
    }

    /// <summary>
    /// The same guarantee for the accepted-URL branch.
    /// </summary>
    [Fact]
    public void EncodeLinkTextWhenUrlIsAccepted()
    {
        var result = Comment("[link text=\"<b>bold</b>\"]https://example.com[/link]");

        result.Should().NotContain("<b>bold</b>");
        result.Should().Contain("&lt;b&gt;bold&lt;/b&gt;");
    }
}
