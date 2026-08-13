using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace DM.Infrastructure.Core.Tracing;

/// <summary>
/// Metrics of writes that were given up on. Everything counted here is data the
/// site decided to lose rather than fail over.
/// </summary>
/// <remarks>
/// The site keeps two stores, and a unit of work spans both: the row is written
/// to PostgreSQL and committed, and the document that goes with it is written to
/// MongoDB afterwards. There is no transaction across the two, and the order is
/// deliberate - the row is the fact, the document is a derived count - so the
/// only two honest answers when the second write fails are to unwind a commit
/// that has already happened, or to keep the fact and lose the count.
///
/// This is the second answer, made visible. Without the counter the failure has
/// no reader at all: the caller's request succeeded, the entity is there, and
/// the only symptom is a badge that is wrong forever for one person - which they
/// will read as the site being wrong about them rather than as an incident.
/// </remarks>
public static class StorageMetrics
{
    /// <summary>Meter name for OTel registration.</summary>
    public const string MeterName = "DM.Storage";

    /// <summary>Name of the document store, as measurements label it.</summary>
    public const string DocumentStore = "mongo";

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
