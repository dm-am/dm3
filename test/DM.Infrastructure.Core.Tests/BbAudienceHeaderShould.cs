using DM.Infrastructure.Core.Parsing;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

public class BbAudienceHeaderShould
{
    [Theory]
    [InlineData(null, RenderAudience.Display)]
    [InlineData("", RenderAudience.Display)]
    [InlineData("   ", RenderAudience.Display)]
    [InlineData("display", RenderAudience.Display)]
    [InlineData("DISPLAY", RenderAudience.Display)]
    [InlineData(" display ", RenderAudience.Display)]
    [InlineData("author_edit", RenderAudience.AuthorEdit)]
    [InlineData("Author_Edit", RenderAudience.AuthorEdit)]
    [InlineData("plain_text", RenderAudience.PlainText)]
    [InlineData("embed_safe", RenderAudience.EmbedSafe)]
    public void ParseKnownValues(string? raw, RenderAudience expected) =>
        BbAudienceHeader.Parse(raw).Should().Be(expected);

    [Theory]
    [InlineData("unknown")]
    [InlineData("admin")]
    [InlineData("bypass")]
    [InlineData("moderation_audit")]
    public void FailSafeToDisplayOnUnknownValue(string raw) =>
        BbAudienceHeader.Parse(raw).Should().Be(RenderAudience.Display);

    [Theory]
    [InlineData(RenderAudience.Display, "display")]
    [InlineData(RenderAudience.AuthorEdit, "author_edit")]
    [InlineData(RenderAudience.PlainText, "plain_text")]
    [InlineData(RenderAudience.EmbedSafe, "embed_safe")]
    public void SerializeCanonicalForm(RenderAudience audience, string expected) =>
        BbAudienceHeader.Serialize(audience).Should().Be(expected);

    [Theory]
    [InlineData(RenderAudience.Display)]
    [InlineData(RenderAudience.AuthorEdit)]
    [InlineData(RenderAudience.PlainText)]
    [InlineData(RenderAudience.EmbedSafe)]
    public void RoundTripThroughSerializeAndParse(RenderAudience audience) =>
        BbAudienceHeader.Parse(BbAudienceHeader.Serialize(audience)).Should().Be(audience);
}
