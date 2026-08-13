using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace DM.Infrastructure.Core.Tracing;

/// <summary>
/// Metrics of the in-process cache.
/// </summary>
/// <remarks>
/// A cache is the one component whose failure mode is being useless rather than
/// being broken: every read still answers, every page still renders, and the only
/// difference between an entry that serves a thousand readers and one nothing
/// ever hits again is a number nobody was counting. Both directions cost - a
/// cache nobody hits is latency and memory spent for nothing, and one everything
/// hits is where a wrong entry is served the longest.
///
/// Counted by the type of what is stored rather than by the key. The keys carry
/// usernames and identifiers, and a label built from one would open a Prometheus
/// series per reader; the type is bounded by the code. The cost of that choice is
/// that two families storing the same shape are counted together.
/// </remarks>
public static class CacheMetrics
{
    /// <summary>Meter name for OTel registration.</summary>
    public const string MeterName = "DM.Cache";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

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
