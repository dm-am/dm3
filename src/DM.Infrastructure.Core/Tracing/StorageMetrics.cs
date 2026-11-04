using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace DM.Infrastructure.Core.Tracing;

/// <summary>
/// Writes given up after their unit of work was committed, such as an unread badge,
/// rather than answer done work with a failure.
/// </summary>
public static class StorageMetrics
{
    /// <summary>Meter name for OTel registration.</summary>
    public const string MeterName = "DM.Storage";

    /// <summary>Name of the relational store, as measurements label it.</summary>
    public const string RelationalStore = "relational";

    private static readonly Meter Meter = new(MeterName, DmActivitySource.Version);

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
