using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.UnreadCounters;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests.UnreadCounters;

/// <summary>
/// Markers written ahead of a relational row that never lands do not survive it.
/// </summary>
/// <remarks>
/// Written first, a refused marker loses an entity nobody has seen yet, and the caller may try again.
/// </remarks>
public class UnreadCountersReservationShould
{
    private static readonly Guid EntityId = Guid.Parse("2c1e5f9a-77b1-4e2d-9c8a-1f0b5d3e7a41");
    private static readonly Guid ParentId = Guid.Parse("8b3d4e6f-2a19-4c7b-8e5d-3f9a1c2b7d60");
    private static readonly Guid[] Readers = [Guid.Parse("d41f7c02-9b8e-4a35-b1c6-5e2d0a7f3b98")];

    private readonly RecordingUnreadCounters counters = new();

    [Fact]
    public async Task TakeBackEveryMarkerWhenNothingIsCommitted()
    {
        await using (await counters.ReserveAsync(
            UnreadMarker.UnderParent(EntityId, ParentId, UnreadEntryType.Message),
            UnreadMarker.ForReaders(EntityId, UnreadEntryType.Character, Readers)))
        {
        }

        counters.Written.Should().HaveCount(2);
        counters.TakenBack.Should().BeEquivalentTo(new[]
        {
            (EntityId, UnreadEntryType.Message, (Guid[]?)null),
            (EntityId, UnreadEntryType.Character, Readers),
        }, "each marker goes back the way it was written, readers and all");
    }

    [Fact]
    public async Task LeaveTheMarkersWhereTheyAreAfterCommit()
    {
        await using (var reservation = await counters.ReserveAsync(
            UnreadMarker.UnderParent(EntityId, ParentId, UnreadEntryType.Message),
            UnreadMarker.ForReaders(EntityId, UnreadEntryType.Character, Readers)))
        {
            reservation.Commit();
        }

        counters.TakenBack.Should().BeEmpty("the row landed");
    }

    // Disposal runs while the caller's own exception is on its way out.
    [Fact]
    public async Task KeepTheCallersFailureWhenTakingBackFailsToo()
    {
        counters.RefuseTakeBack = () => new TimeoutException("the document store is not answering");

        var caller = async () =>
        {
            await using var reservation = await counters.ReserveAsync(
                UnreadMarker.UnderParent(EntityId, ParentId, UnreadEntryType.Message));
            throw new InvalidOperationException("storage refused");
        };

        (await caller.Should().ThrowAsync<InvalidOperationException>(
            "the refusal of the store is what the caller has to see, not the refusal " +
            "of the cleanup that followed it")).WithMessage("storage refused");
    }

    /// <summary>
    /// A caller never sees a half-written set.
    /// </summary>
    [Fact]
    public async Task TakeBackWhatLandedWhenAMarkerWriteFailsMidway()
    {
        counters.RefuseWriteOf = () => new TimeoutException("the document store is not answering");
        counters.RefusedEntryType = UnreadEntryType.Character;

        var reserve = async () => await counters.ReserveAsync(
            UnreadMarker.UnderParent(EntityId, ParentId, UnreadEntryType.Message),
            UnreadMarker.ForReaders(EntityId, UnreadEntryType.Character, Readers));

        await reserve.Should().ThrowAsync<TimeoutException>();
        counters.TakenBack.Should().ContainSingle()
            .Which.Should().Be((EntityId, UnreadEntryType.Message, (Guid[]?)null));
    }

    /// <remarks>
    /// The same people have to be written and, if the write is taken back, taken
    /// back. A sequence enumerated a second time may answer differently, and the
    /// difference would be a marker nobody ever collects.
    /// </remarks>
    [Fact]
    public void MaterialiseTheReadersItWasGiven()
    {
        var reads = 0;
        IEnumerable<Guid> Counted()
        {
            reads++;
            yield return Readers[0];
        }

        var marker = UnreadMarker.ForReaders(EntityId, UnreadEntryType.Message, Counted());

        reads.Should().Be(1);
        marker.Readers.Should().BeEquivalentTo(Readers);
        marker.Readers.Should().BeEquivalentTo(Readers, "and again, off the same materialised list");
        reads.Should().Be(1);
    }
}
