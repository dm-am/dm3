using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace DM.Infrastructure.Core.Tracing;

/// <summary>
/// Metrics of the in-process cache.
/// </summary>
/// <remarks>
/// Counted by the type of the entry, not by the key, which carries user names and
/// would open a series per reader.
/// </remarks>
public static class CacheMetrics
{
    /// <summary>Meter name for OTel registration.</summary>
    public const string MeterName = "DM.Cache";

    private static readonly Meter Meter = new(MeterName, DmActivitySource.Version);

    /// <summary>
    /// Cache lookups. Attributes: <c>result</c> (hit or miss), <c>entry</c> (type
    /// of the cached value).
    /// </summary>
    public static readonly Counter<long> Requests = Meter.CreateCounter<long>(
        "dm.cache.requests", null, "Cache lookups by result");

    /// <summary>Attribute set naming how a lookup ended.</summary>
    /// <param name="hit">Whether the value was already there.</param>
    public static KeyValuePair<string, object?> Result(bool hit) => new("result", hit ? "hit" : "miss");

    /// <summary>Attribute set naming what was looked up.</summary>
    /// <param name="entry">Type of the cached value.</param>
    public static KeyValuePair<string, object?> Entry(string entry) => new("entry", entry);
}
