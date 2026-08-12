using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// A body the document does not name is read as a body, not as an absence.
/// </summary>
/// <remarks>
/// Asserted on written-out fragments because the document holds no such response
/// today: over it the three tests that read a response body answer the same
/// either way, and the day one appears is the day the difference is the whole
/// point. Written out here rather than left to that day, because the reading is
/// what those three tests are: silence about a bare array is indistinguishable
/// from a clean run.
/// </remarks>
public class ResponseBodySchemaShould
{
    /// <summary>A response object of the document, written out here.</summary>
    private static JsonElement Fragment(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Fact]
    public void ReadANamedBodyByItsName() =>
        ResponseBodySchema.NameOf(Fragment(
                """{"content":{"application/json":{"schema":{"$ref":"#/components/schemas/DM.Web.API.Shared.Dto.EnvelopeOfRoom"}}}}"""))
            .Should().Be("DM.Web.API.Shared.Dto.EnvelopeOfRoom",
                "the callers ask whether the name is an envelope, and the registry prefix " +
                "in front of it is the same on every answer");

    [Fact]
    public void ReadABodyWrittenInPlaceAsAnUnnamedBody() =>
        ResponseBodySchema.NameOf(Fragment(
                """{"content":{"application/json":{"schema":{"type":"array","items":{"type":"string"}}}}}"""))
            .Should().Be(ResponseBodySchema.WrittenInPlace,
                "an array of resources is not an envelope of one, and answering null here " +
                "is what let such an operation out of every set these tests enumerate");

    [Fact]
    public void ReadAResponseWithoutABodyAsNothing() =>
        ResponseBodySchema.NameOf(Fragment("""{"description":"No Content"}"""))
            .Should().BeNull("a response with no body has nothing to wrap");

    [Fact]
    public void TellAnArrayWrittenInPlaceFromTheRest()
    {
        ResponseBodySchema.IsArray(Fragment(
                """{"content":{"application/json":{"schema":{"type":"array","items":{"type":"string"}}}}}"""))
            .Should().BeTrue("this is the shape of a list with no envelope around it");

        ResponseBodySchema.IsArray(Fragment(
                """{"content":{"application/json":{"schema":{"type":"string"}}}}"""))
            .Should().BeFalse("one value is not a list");

        ResponseBodySchema.IsArray(Fragment("""{"description":"No Content"}"""))
            .Should().BeFalse("a response with no body is not a list either");
    }
}
