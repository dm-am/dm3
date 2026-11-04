using AwesomeAssertions;
using DM.Infrastructure.Core.Parsing;
using Xunit;
using static DM.Infrastructure.Core.Tests.BbAudienceRender;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// The head of [spoiler] and [nsfw]: its words, the caption the author gave it,
/// and no javascript: address (BBCODE_RENDERING.md, "Набор тегов").
/// </summary>
public class BbSpoilerTagShould
{
    [Fact]
    public void ConvertSimpleSpoilerToHtmlUsesDefaultText()
    {
        var result = Comment("[spoiler]hidden content[/spoiler]");

        result.Should().Contain("class=\"spoiler-head\"");
        result.Should().Contain(">Показать содержимое</a>");
        result.Should().Contain("hidden content");
    }

    /// <summary>
    /// The caption is author text written into element text, so it is encoded there.
    /// </summary>
    [Fact]
    public void EncodeTheCaptionOfASpoilerAndKeepItsSpaces()
    {
        var result = Comment("[spoiler text=\"Карта <b>мира</b> и \"еще\"\"]x[/spoiler]");

        result.Should().Contain(">Карта &lt;b&gt;мира&lt;/b&gt; и &quot;еще&quot;</a>");
        result.Should().NotContain("<b>");
    }

    /// <summary>
    /// No javascript: URL leaves this renderer.
    /// </summary>
    /// <remarks>A javascript: href is inline script to CSP; the client preventDefaults both toggles.</remarks>
    [Theory]
    [InlineData("[spoiler]hidden[/spoiler]")]
    [InlineData("[nsfw]shocking[/nsfw]")]
    [InlineData("[img]https://example.com/a.png[/img]")]
    public void EmitNoJavascriptUrl(string input)
    {
        Html(BbSurface.GlobalChatMessage, input).Should().NotContain("javascript:");
        Comment(input).Should().NotContain("javascript:");
    }
}
