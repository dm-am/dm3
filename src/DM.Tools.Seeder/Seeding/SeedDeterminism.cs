using System;
using System.Globalization;
using DM.Domain.Core.Abstractions;
using Microsoft.Extensions.Configuration;

namespace DM.Tools.Seeder.Seeding;

/// <remarks>
/// The only file of the tool allowed a clock or a generator, which SeedDeterminismShould holds:
/// a stray clock read makes two runs disagree on every date. An unparseable value fails loudly.
/// </remarks>
internal sealed class SeedDeterminism
{
    /// <summary>
    /// Environment form: <c>DM_SeedEpochUtc</c>.
    /// </summary>
    public const string EpochKey = "SeedEpochUtc";

    /// <summary>
    /// Configuration key holding the seed of <see cref="Random"/> and of the
    /// identifiers. Environment form: <c>DM_SeedRandomSeed</c>.
    /// </summary>
    public const string RandomSeedKey = "SeedRandomSeed";

    // Arbitrary, and fixed forever.
    private const int DefaultRandomSeed = 20260730;

    public SeedDeterminism(IConfiguration configuration, IDateTimeProvider dateTimeProvider)
    {
        Epoch = ResolveEpoch(configuration, dateTimeProvider);
        RandomSeed = ResolveRandomSeed(configuration);
        Random = new Random(RandomSeed);
    }

    /// <remarks>
    /// The real clock by default: a fixed past epoch would empty every surface built around recency.
    /// <see cref="EpochKey"/> pins it, for a pixel baseline.
    /// </remarks>
    public DateTimeOffset Epoch { get; }

    public int RandomSeed { get; }

    /// <remarks>
    /// Not thread-safe: the seed runs one aggregate after another on a single DmDbContext.
    /// </remarks>
    public Random Random { get; }

    /// <exception cref="FormatException">The key was set to something unparseable.</exception>
    private static DateTimeOffset ResolveEpoch(
        IConfiguration configuration, IDateTimeProvider dateTimeProvider)
    {
        var configured = configuration[EpochKey];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return dateTimeProvider.Now;
        }

        return DateTimeOffset.Parse(
            configured,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
    }

    /// <exception cref="FormatException">The key was set to something that is not an integer.</exception>
    private static int ResolveRandomSeed(IConfiguration configuration)
    {
        var configured = configuration[RandomSeedKey];
        return string.IsNullOrWhiteSpace(configured)
            ? DefaultRandomSeed
            : int.Parse(configured, CultureInfo.InvariantCulture);
    }
}

/// <remarks>
/// A public identifier is cut out of a Guid, so with Guid.NewGuid() a page moved every run.
/// Its own generator: a new identifier then shifts no content choice. Not thread-safe.
/// </remarks>
internal sealed class SeededGuidFactory : IGuidFactory
{
    private readonly Random _random;

    public SeededGuidFactory(SeedDeterminism determinism) =>
        _random = new Random(determinism.RandomSeed);

    /// <inheritdoc />
    public Guid Create()
    {
        Span<byte> value = stackalloc byte[16];
        _random.NextBytes(value);
        return new Guid(value);
    }
}
