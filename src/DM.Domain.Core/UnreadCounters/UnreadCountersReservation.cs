using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Core.UnreadCounters;

/// <summary>
/// Markers written ahead of the relational row they belong to, and taken back if
/// that row never lands.
/// </summary>
/// <remarks>
/// <para>
/// There is no transaction across the two stores and no outbox — DATA_STORAGE.md
/// says both in as many words — so a feature living in both owes an explicit
/// order. The markers go first, because their identifiers are minted by the
/// caller and need no round trip to learn. Written after the insert instead, a
/// refused write to the document store left a committed entity whose counters do
/// not exist and never will: nothing recreates them, and its badge reads zero for
/// everybody forever. Written first, the same refusal loses an entity nobody has
/// seen yet, and the caller may simply try again.
/// </para>
/// <para>
/// Taking them back is part of the write path rather than a habit of each caller:
/// the order was correct in exactly one of the six places that needed it, and the
/// other five were written by copying a neighbour.
/// </para>
/// <para>
/// A failure to take a marker back is swallowed, and that is the deliberate half.
/// This runs from disposal, which happens while the caller's own exception is on
/// its way out — the refusal of the store is what the caller has to see, not the
/// refusal of the cleanup that followed it. What survives such a double failure
/// is a marker under an entity that was never committed: it cannot be opened by
/// anybody, and it inflates one parent-scoped total until the expiry index that
/// collects removed markers reaches it.
/// </para>
/// </remarks>
public sealed class UnreadCountersReservation : IAsyncDisposable
{
    private readonly IUnreadCountersRepository repository;
    private readonly List<UnreadMarker> written = [];
    private bool committed;

    internal UnreadCountersReservation(IUnreadCountersRepository repository) => this.repository = repository;

    /// <summary>
    /// Writes one marker and keeps it on the list of what has to be taken back.
    /// </summary>
    internal async Task WriteAsync(UnreadMarker marker)
    {
        if (marker.Readers == null)
        {
            await repository.CreateMarkerAsync(marker.EntityId, marker.ParentId, marker.EntryType);
        }
        else
        {
            await repository.CreateMarkerAsync(marker.EntityId, marker.EntryType, marker.Readers);
        }

        written.Add(marker);
    }

    /// <summary>
    /// The relational row landed: the markers stay where they are.
    /// </summary>
    public void Commit() => committed = true;

    /// <summary>
    /// Takes back every marker written under a reservation nobody committed.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (committed)
        {
            return;
        }

        foreach (var marker in written)
        {
            try
            {
                if (marker.Readers == null)
                {
                    await repository.DeleteAsync(marker.EntityId, marker.EntryType);
                }
                else
                {
                    await repository.DeleteAsync(marker.EntityId, marker.EntryType, marker.Readers);
                }
            }
            catch
            {
                // See the class remarks: the caller's exception outranks this one.
            }
        }

        written.Clear();
    }
}
