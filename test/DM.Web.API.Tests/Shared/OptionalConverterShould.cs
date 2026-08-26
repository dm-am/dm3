using System;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Web.API.Shared.Binding;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// Optional carries three states across the wire, and PATCH needs all three.
/// </summary>
/// <remarks>
/// Property absent means "leave it alone", property null means "clear it",
/// property set means "assign it". The domain reads exactly that —
/// ShouldReorder is PreviousRoomId != null, the new value is
/// PreviousRoomId?.Value — but the converter could not produce the middle state:
/// Optional is a class, so with the default HandleNull the serialiser skipped
/// Read on a null and wrote null into the property itself. A room could not be
/// moved to the head of the chain through the API, because the request that says
/// so was indistinguishable from the request that says nothing.
/// </remarks>
public class OptionalConverterShould
{
    private class Target
    {
        public Optional<Guid>? PreviousRoomId { get; set; }

        public Optional<int>? Position { get; set; }
    }

    private static readonly JsonSerializerOptions Options = BuildOptions();

    private static JsonSerializerOptions BuildOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new OptionalConverterFactory());
        return options;
    }

    [Fact]
    public void ReadAnAbsentPropertyAsNoChange()
    {
        var target = JsonSerializer.Deserialize<Target>("""{}""", Options)!;

        target.PreviousRoomId.Should().BeNull("an absent property asks for nothing");
    }

    [Fact]
    public void ReadAnExplicitNullAsClear()
    {
        var target = JsonSerializer.Deserialize<Target>(
            """{"previousRoomId":null}""", Options)!;

        target.PreviousRoomId.Should().NotBeNull("an explicit null is a value, not an absence");
        target.PreviousRoomId!.Value.Should().BeNull();
    }

    [Fact]
    public void ReadAValueAsAssign()
    {
        var id = Guid.NewGuid();

        var target = JsonSerializer.Deserialize<Target>(
            $$"""{"previousRoomId":"{{id}}"}""", Options)!;

        target.PreviousRoomId!.Value.Should().Be(id);
    }

    [Fact]
    public void ReadANonStringValue()
    {
        // Reading a string first threw here, and the throw left the pipeline as a
        // 500. No Optional<int> is on the wire today; the trap was set for the
        // next person to add one.
        var target = JsonSerializer.Deserialize<Target>("""{"position":7}""", Options)!;

        target.Position!.Value.Should().Be(7);
    }
}
