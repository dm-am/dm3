using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace DM.Infrastructure.Core.Tracing;

/// <summary>
/// Metrics of writes that were given up on. Everything counted here is data the
/// site decided to lose rather than fail over.
/// </summary>
/// <remarks>
/// The increments of the unread badge run after the unit of work they belong to
/// has already been committed: the row is the fact, the counter is a derived
/// number. An error raised there would answer the caller with a failure for
/// work that was in fact done, so the write is given up and counted here
/// instead. Without the counter the failure has no reader at all: the caller's
/// request succeeded, the entity is there, and the only symptom is a badge that
/// is wrong forever for one person - which they will read as the site being
/// wrong about them rather than as an incident.
/// </remarks>
public static class StorageMetrics
{
    /// <summary>Meter name for OTel registration.</summary>
    public const string MeterName = "DM.Storage";

    /// <summary>Name of the relational store, as measurements label it.</summary>
    public const string RelationalStore = "relational";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>
    /// Writes abandoned after the work they belong to was already committed
    /// elsewhere. Attributes: <c>store</c>, <c>operation</c> (what was being
    /// written), <c>reason</c> (exception type).
    /// </summary>
    public static readonly Counter<long> WriteLost = Meter.CreateCounter<long>(
        "dm.storage.write_lost", null, "Writes given up after the work they belong to was committed");

    /// <summary>Attribute set naming the store a write was lost in.</summary>
    /// <param name="store">Store name.</param>
    public static KeyValuePair<string, object?> Store(string store) => new("store", store);

    /// <summary>Attribute set naming what was being written.</summary>
    /// <param name="operation">Operation name.</param>
    public static KeyValuePair<string, object?> Operation(string operation) => new("operation", operation);
}
