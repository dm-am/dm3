using System;
using System.Globalization;
using DM.Domain.Core.Abstractions;
using Microsoft.Extensions.Configuration;

namespace DM.Tools.Seeder.Seeding;

/// <summary>
/// The two values the whole fixture is a function of: the instant it is laid out
/// around, and the seed its randomness is drawn from.
/// </summary>
/// <remarks>
/// One object, because a second reader of either value is a second default, and a
/// fixture with two defaults is reproducible on one machine only. Both are read
/// from configuration and both fail loudly on an unparseable value: a typo that
/// quietly fell back would look like a determinism bug in whatever consumed the
/// seed rather than like the typo it is.
///
/// This is also the only file in the tool allowed to read a clock or to build a
/// generator, and SeedDeterminismShould holds it to that. The point is not
/// tidiness: a screenshot baseline is worth exactly as much as the fixture under
/// it is repeatable, and a single stray clock read is enough to make two runs
/// disagree on every date they carry.
/// </remarks>
internal sealed class SeedDeterminism
{
    /// <summary>
    /// Configuration key holding the instant the whole seed is laid out around.
    /// Environment form: <c>DM_SeedEpochUtc</c>.
    /// </summary>
    public const string EpochKey = "SeedEpochUtc";

    /// <summary>
    /// Configuration key holding the seed of <see cref="Random"/> and of the
    /// identifiers. Environment form: <c>DM_SeedRandomSeed</c>.
    /// </summary>
    public const string RandomSeedKey = "SeedRandomSeed";

    /// <summary>
    /// Seed used when the key is unset. Arbitrary value, fixed forever: what
    /// matters is that it never changes, not what it is.
    /// </summary>
    private const int DefaultRandomSeed = 20260730;

    /// <summary>
    /// Creates a new instance of <see cref="SeedDeterminism"/>
    /// </summary>
    public SeedDeterminism(IConfiguration configuration, IDateTimeProvider dateTimeProvider)
    {
        Epoch = ResolveEpoch(configuration, dateTimeProvider);
        RandomSeed = ResolveRandomSeed(configuration);
        Random = new Random(RandomSeed);
    }

    /// <summary>
    /// The instant every seeded timestamp is offset from.
    /// </summary>
    /// <remarks>
    /// Resolved once so that a single run is internally consistent, and
    /// overridable through <see cref="EpochKey"/> so that a run can be pinned to
    /// a chosen moment. It defaults to the real clock on purpose: a hardcoded
    /// past epoch would empty every surface built around recency - the
    /// current-month leaderboards, "activated N days ago", the new-games block.
    /// Pinning is what a pixel baseline needs, and only it.
    /// </remarks>
    public DateTimeOffset Epoch { get; }

    /// <summary>
    /// The seed <see cref="Random"/> and the identifiers were built from.
    /// </summary>
    public int RandomSeed { get; }

    /// <summary>
    /// The only source of randomness in the content of the seed, and a seeded one.
    /// </summary>
    /// <remarks>
    /// Every draw here reaches something a reader sees: which accounts play which
    /// game, who wrote a comment and what it says, view and like counts, review
    /// verdicts, list order. On <c>Random.Shared</c> that made the fixture
    /// different on every run, so an assertion about a count or a name held or
    /// failed by luck and nothing could be measured twice. Seeded, the same
    /// command produces the same fixture on any machine.
    ///
    /// Not thread-safe, and does not need to be: the seed runs one aggregate
    /// after another on a single scope, because a shared <c>DmDbContext</c>
    /// cannot be used concurrently either.
    /// </remarks>
    public Random Random { get; }

    /// <summary>
    /// Reads the pinned epoch, falling back to the clock.
    /// </summary>
    /// <exception cref="FormatException">
    /// The key was set to something unparseable. Thrown rather than ignored: a
    /// typo that silently reverted to the clock would look like a determinism
    /// bug in whatever consumed the seed.
    /// </exception>
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

    /// <summary>
    /// Reads the pinned seed, falling back to <see cref="DefaultRandomSeed"/>.
    /// </summary>
    /// <exception cref="FormatException">
    /// The key was set to something that is not an integer. Thrown for the same
    /// reason the epoch is: a run that quietly used a different seed than the one
    /// asked for produces a different fixture and reports success.
    /// </exception>
    private static int ResolveRandomSeed(IConfiguration configuration)
    {
        var configured = configuration[RandomSeedKey];
        return string.IsNullOrWhiteSpace(configured)
            ? DefaultRandomSeed
            : int.Parse(configured, CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Identifiers drawn from the seed instead of from the operating system.
/// </summary>
/// <remarks>
/// The infrastructure factory is <c>Guid.NewGuid()</c>, which is exactly what a
/// fixture must not have. The ten-character public identifier of a blog notepad
/// and of a game chat is cut straight out of one, so on every run the same page
/// lived at a different address: a route could not be written down, let alone
/// screenshotted. Registered over the infrastructure default in the seeder's
/// container only - nothing else resolves this type, and the tool is never part
/// of a running site.
///
/// Its own generator rather than a share of <see cref="SeedDeterminism.Random"/>,
/// built from the same configured seed: the two streams are consumed differently
/// (whole objects here, bounded draws there), and keeping them apart is what lets
/// an identifier be added to the seed without shifting every content choice made
/// after it.
///
/// Not thread-safe, like the generator it wraps, and for the same reason: the
/// seed runs one aggregate after another on a single scope.
/// </remarks>
internal sealed class SeededGuidFactory : IGuidFactory
{
    private readonly Random _random;

    /// <summary>
    /// Creates a new instance of <see cref="SeededGuidFactory"/>
    /// </summary>
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
