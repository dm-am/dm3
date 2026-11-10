namespace DM.Domain.Core.UnreadCounters;

/// <summary>
/// Markers written ahead of the relational row they belong to, and taken back if
/// that row never lands.
/// </summary>
/// <remarks>
/// <para>
/// Markers go first: a refused marker write after the insert would leave a
/// committed entity whose counters never exist, while before it the caller may
/// simply try again.
/// </para>
/// <para>
/// A failure to take a marker back is swallowed: disposal runs while the caller's
/// own exception is on its way out, and that is the one the caller has to see.
/// The leftover marker inflates one total until the retention sweep removes it.
/// </para>
/// </remarks>
public sealed class UnreadCountersReservation : IAsyncDisposable
{
    private readonly IUnreadCountersRepository repository;
    private readonly List<UnreadMarker> written = [];
    private bool committed;

    internal UnreadCountersReservation(IUnreadCountersRepository repository) => this.repository = repository;

    // Writes one marker and keeps it on the list of what has to be taken back.
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
