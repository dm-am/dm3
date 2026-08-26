using System;
using System.Linq;
using DM.Infrastructure.Core.Parsing;
using AwesomeAssertions;
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

    /// <summary>
    /// The audiences a client may name are exactly the ones the header spells.
    /// </summary>
    /// <remarks>
    /// The enum carries one more - the intent behind a quotation, which emits
    /// BBCode source rather than HTML and is chosen by the endpoint that
    /// composes one. Reachable through the header, it would put author text no
    /// security substitution has run over into fields the client binds through
    /// v-html, on any endpoint, for any reader.
    ///
    /// So the list is written out by hand, and this is what keeps writing it out
    /// from being a thing to remember: a new audience added to the enum is not
    /// on the wire until somebody adds it here as well, and a value on this list
    /// that Parse does not answer with fails right here.
    /// </remarks>
    [Fact]
    public void OfferOnWireExactlyWhatParseAnswersWith()
    {
        BbAudienceHeader.Wire.Should().NotContain(RenderAudience.QuoteSource,
            "a quotation source is composed by an endpoint, never asked for by a header");

        foreach (var audience in BbAudienceHeader.Wire)
        {
            BbAudienceHeader.Parse(BbAudienceHeader.Serialize(audience))
                .Should().Be(audience);
        }

        foreach (var audience in Enum.GetValues<RenderAudience>())
        {
            if (BbAudienceHeader.Wire.Contains(audience)) continue;

            var serialize = () => BbAudienceHeader.Serialize(audience);
            serialize.Should().Throw<ArgumentOutOfRangeException>(
                "{0} is not an audience a client may name, so it has no wire value", audience);
        }
    }
}
