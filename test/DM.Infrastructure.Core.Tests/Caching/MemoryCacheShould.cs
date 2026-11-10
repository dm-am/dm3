using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Caching;

// A cache nothing hits answers every read like a working one, so its hit rate is measured.
public class MemoryCacheShould
{
    [Fact]
    public async Task SayWhetherItAlreadyHadWhatItWasAskedFor()
    {
        var results = new List<string?>();
        using var listener = CounterTags.Listen("dm.cache.requests", "result", results);

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
        using var listener = CounterTags.Listen("dm.cache.requests", "entry", entries);

        var cache = Cache();

        await cache.GetOrCreateAsync("key",
            () => Task.FromResult((first: 1, second: 2)), TimeSpan.FromMinutes(5));

        entries.Should().Equal(["ValueTuple"],
            "a label carrying the backtick of a generic is a label nobody selects on by hand");
    }

    // The cache under test, over a real store rather than a stand-in.
    private static DM.Infrastructure.Core.Caching.MemoryCache Cache() =>
        new(new Microsoft.Extensions.Caching.Memory.MemoryCache(new MemoryCacheOptions()));
}
