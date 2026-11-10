using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Geo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Infrastructure.Core.Geo;

/// <inheritdoc />
/// <remarks>
/// A sorted table of ranges in memory: an address sent to a service would leave the
/// site to answer a question about its reader. Read on the first question, since
/// only one host asks. A question takes the table once, so a replacement
/// (<see cref="Use"/>) never mixes two tables. IPv4 apart from IPv6, at a quarter
/// of the size.
/// </remarks>
internal sealed class IpCountryLookup : IIpCountryLookup
{
    private readonly ILogger<IpCountryLookup> _logger;
    private readonly string _path;

    /// <summary>
    /// So that a slow first read cannot land over a table replaced while it ran.
    /// </summary>
    private readonly Lock _gate = new();

    private Table? _table;

    public IpCountryLookup(
        IOptions<GeoLocationConfiguration> configuration,
        ILogger<IpCountryLookup> logger)
    {
        _logger = logger;
        _path = configuration.Value.CountryDatabasePath;
    }

    /// <summary>
    /// Whether the table in use answers anything. Reads the configured file
    /// first if nothing has been read yet.
    /// </summary>
    internal bool HoldsRanges => Current.RangeCount > 0;

    /// <inheritdoc />
    public string? Find(string? ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var parsed)) return null;

        var table = Current;
        var code = parsed.AddressFamily switch
        {
            AddressFamily.InterNetwork => Search(table.V4, ToV4(parsed)),
            AddressFamily.InterNetworkV6 => Search(table.V6, ToV6(parsed)),
            _ => null
        };

        return code is null ? null : table.Names.GetValueOrDefault(code);
    }

    private static string? Search<T>(Range<T>[] ranges, T address) where T : IComparable<T>
    {
        var low = 0;
        var high = ranges.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var range = ranges[middle];
            if (address.CompareTo(range.First) < 0) high = middle - 1;
            else if (address.CompareTo(range.Last) > 0) low = middle + 1;
            else return range.Code;
        }

        return null;
    }

    internal void Use(Table table)
    {
        lock (_gate)
        {
            Volatile.Write(ref _table, table);
        }
    }

    private Table Current => Volatile.Read(ref _table) ?? LoadConfigured();

    private Table LoadConfigured()
    {
        lock (_gate)
        {
            var held = _table;
            if (held is not null) return held;

            var loaded = ReadConfigured();
            Volatile.Write(ref _table, loaded);
            return loaded;
        }
    }

    /// <remarks>
    /// A missing file is not worth a failed start, one line of one letter, but a warning.
    /// </remarks>
    private Table ReadConfigured()
    {
        if (string.IsNullOrWhiteSpace(_path))
        {
            _logger.LogInformation(
                "No country table configured; letters will name no country");
            return Table.Empty;
        }

        if (!File.Exists(_path))
        {
            _logger.LogWarning(
                "Country table not found at {Path}; letters will name no country", _path);
            return Table.Empty;
        }

        var table = Read(_path, out var skipped);
        if (table is null)
        {
            _logger.LogWarning(
                "Country table at {Path} holds no usable range; letters will name no country",
                _path);
            return Table.Empty;
        }

        _logger.LogInformation(
            "Country table loaded from {Path}: {V4} IPv4 ranges, {V6} IPv6 ranges, {Skipped} lines skipped",
            _path, table.V4.Length, table.V6.Length, skipped);

        return table;
    }

    /// <summary>
    /// The table a file holds, or null when not one line of it is a usable range.
    /// </summary>
    /// <param name="skipped">Lines that were not a range and were left out.</param>
    internal static Table? Read(string path, out int skipped)
    {
        var v4 = new List<Range<uint>>();
        var v6 = new List<Range<UInt128>>();
        skipped = 0;

        foreach (var line in File.ReadLines(path))
        {
            var fields = line.Split(',');
            if (fields.Length < 3 ||
                !IPAddress.TryParse(Unquote(fields[0]), out var first) ||
                !IPAddress.TryParse(Unquote(fields[1]), out var last) ||
                first.AddressFamily != last.AddressFamily)
            {
                skipped++;
                continue;
            }

            var code = Unquote(fields[2]).ToUpperInvariant();
            if (code.Length != 2)
            {
                skipped++;
                continue;
            }

            if (first.AddressFamily == AddressFamily.InterNetwork)
                v4.Add(new Range<uint>(ToV4(first), ToV4(last), code));
            else
                v6.Add(new Range<UInt128>(ToV6(first), ToV6(last), code));
        }

        if (v4.Count + v6.Count == 0) return null;

        return new Table(
            v4.OrderBy(range => range.First).ToArray(),
            v6.OrderBy(range => range.First).ToArray(),
            RussianCountryNames());
    }

    /// <summary>
    /// Country names in Russian, taken from the culture data of the runtime.
    /// </summary>
    /// <remarks>
    /// Not RegionInfo, whose DisplayName is the country's own name ("Deutschland"):
    /// the name of a culture under ru-RU, "немецкий (Германия)", holds the country in
    /// Russian. A code no culture names is left out, and the letter names no country.
    /// </remarks>
    private static Dictionary<string, string> RussianCountryNames()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
        try
        {
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                string code;
                try
                {
                    code = new RegionInfo(culture.Name).TwoLetterISORegionName;
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (code.Length != 2 || names.ContainsKey(code)) continue;

                var country = CountryOf(culture.DisplayName);
                if (!string.IsNullOrWhiteSpace(country)) names[code] = country;
            }

            return names;
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    /// <summary>
    /// The country out of a culture's name: the last thing in the parentheses.
    /// </summary>
    /// <remarks>
    /// A script comes first: "сербский (латиница, Сербия)".
    /// </remarks>
    private static string? CountryOf(string cultureName)
    {
        var open = cultureName.LastIndexOf('(');
        var close = cultureName.LastIndexOf(')');
        if (open < 0 || close < open) return null;

        var inside = cultureName[(open + 1)..close];
        var comma = inside.LastIndexOf(", ", StringComparison.Ordinal);
        return comma < 0 ? inside : inside[(comma + 2)..];
    }

    /// <summary>Some publishers quote every field, some quote none.</summary>
    private static string Unquote(string field) => field.Trim().Trim('"');

    // Big-endian, so that numeric order is address order.
    private static uint ToV4(IPAddress address)
    {
        Span<byte> bytes = stackalloc byte[4];
        address.TryWriteBytes(bytes, out _);
        return BinaryPrimitives.ReadUInt32BigEndian(bytes);
    }

    private static UInt128 ToV6(IPAddress address)
    {
        Span<byte> bytes = stackalloc byte[16];
        address.TryWriteBytes(bytes, out _);
        return BinaryPrimitives.ReadUInt128BigEndian(bytes);
    }

    internal readonly record struct Range<T>(T First, T Last, string Code) where T : IComparable<T>;

    internal sealed record Table(
        Range<uint>[] V4,
        Range<UInt128>[] V6,
        Dictionary<string, string> Names)
    {
        public static Table Empty { get; } = new([], [], new Dictionary<string, string>(StringComparer.Ordinal));

        public int RangeCount => V4.Length + V6.Length;
    }
}
