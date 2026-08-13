using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Caching;

/// <summary>
/// The cache says how much of what it is asked for it already had.
/// </summary>
/// <remarks>
/// This is the one component whose failure is being useless rather than being
/// broken. Every read still answers, every page still renders, and a cache that
/// nothing ever hits again — a key that grew a field, a lifetime shorter than the
/// interval between requests — is indistinguishable from a working one by every
/// other signal the process publishes. Both ends of that cost: nothing hit is
/// latency and memory spent for no return, everything hit is where a stale entry
/// is served the longest.
/// </remarks>
public class MemoryCacheShould
{
    [Fact]
    public async Task SayWhetherItAlreadyHadWhatItWasAskedFor()
    {
        var results = new List<string?>();
        using var listener = Listening(results);

        var cache = Cache();

        await cache.GetOrCreateAsync("key", () => Task.FromResult("value"), TimeSpan.FromMinutes(5));
        await cache.GetOrCreateAsync("key", () => Task.FromResult("value"), TimeSpan.FromMinutes(5));

        results.Should().Equal(["miss", "hit"],
            "the first lookup had nothing to return and the second had, and the difference " +
            "between a cache that works and one that stopped working is exactly this ratio");
    }

    [Fact]
    public async Task NameTheShapeItStoredWithoutTheArityOfAGeneric()
    {
        var entries = new List<string?>();
        using var listener = Listening(entries, tag: "entry");

        var cache = Cache();

        await cache.GetOrCreateAsync("key",
            () => Task.FromResult((first: 1, second: 2)), TimeSpan.FromMinutes(5));

        entries.Should().Equal(["ValueTuple"],
            "a label carrying the backtick of a generic is a label nobody selects on by hand");
    }

    /// <summary>The cache under test, over a real store rather than a stand-in.</summary>
    private static DM.Infrastructure.Core.Caching.MemoryCache Cache() =>
        new(new Microsoft.Extensions.Caching.Memory.MemoryCache(new MemoryCacheOptions()));

    private static MeterListener Listening(ICollection<string?> collected, string tag = "result")
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, self) =>
            {
                if (instrument.Name == "dm.cache.requests")
                {
                    self.EnableMeasurementEvents(instrument);
                }
            },
        };

        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var measured in tags)
            {
                if (measured.Key == tag)
                {
                    collected.Add(measured.Value?.ToString());
                }
            }
        });

        listener.Start();
        return listener;
    }
}
