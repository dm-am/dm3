using DM.Infrastructure.Core.Parsing;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// Contract tests for the X-Dm-Audience header parser. Guarantees
/// the semantic audience vocabulary (display / author_edit / plain_text
/// / embed_safe) stays stable and tolerates whitespace, casing, and
/// unknown values (fail-safe to Display).
/// </summary>
public class BbAudienceHeaderShould
{
    [Fact]
    public void HaveConsistentHeaderName() =>
        BbAudienceHeader.HeaderName.Should().Be("X-Dm-Audience");

    [Fact]
    public void DefaultToDisplayAudience() =>
        BbAudienceHeader.Default.Should().Be(RenderAudience.Display);

    [Theory]
    [InlineData(null, RenderAudience.Display)]
    [InlineData("", RenderAudience.Display)]
    [InlineData("   ", RenderAudience.Display)]
    [InlineData("display", RenderAudience.Display)]
    [InlineData("DISPLAY", RenderAudience.Display)]
    [InlineData(" display ", RenderAudience.Display)]
    [InlineData("author_edit", RenderAudience.AuthorEdit)]
    [InlineData("authoredit", RenderAudience.AuthorEdit)]
    [InlineData("Author_Edit", RenderAudience.AuthorEdit)]
    [InlineData("plain_text", RenderAudience.PlainText)]
    [InlineData("plaintext", RenderAudience.PlainText)]
    [InlineData("text", RenderAudience.PlainText)]
    [InlineData("embed_safe", RenderAudience.EmbedSafe)]
    [InlineData("embedsafe", RenderAudience.EmbedSafe)]
    [InlineData("safehtml", RenderAudience.EmbedSafe)]
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
