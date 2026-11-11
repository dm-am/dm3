using System.Diagnostics;

namespace DM.Infrastructure.Core.Queues;

/// <summary>
/// The trace context an outbox row carries, so the worker's span continues the request's trace.
/// </summary>
public static class StoredTraceParent
{
    /// <summary>
    /// The current trace identifier when it is in the W3C format, otherwise null.
    /// </summary>
    /// <remarks>
    /// Any other format stays out of the row: a value that made the insert fail would lose the row.
    /// </remarks>
    public static string? Current() =>
        Activity.Current is { IdFormat: ActivityIdFormat.W3C } current ? current.Id : null;
}
