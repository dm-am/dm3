using System;
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
/// A sorted table of ranges and a binary search over it, held in memory. No
/// package and no network call: the format is a line of three fields, and an
/// address sent to somebody else's service on every suspicious login would be
/// the reader's address leaving the site to answer a question about the reader.
///
/// Read from the file on the first question rather than at startup. Four hosts
/// share this registration and one of them sends the letter; the other three
/// would pay for a table they never ask. The one that sends it also runs the
/// refresh, which reads the file as soon as the host is up.
///
/// The table can be replaced while the process runs (<see cref="Use"/>), which is
/// how <see cref="CountryTableRefresher"/> keeps it fresh without a restart. A
/// question takes the table once and answers from it, so a replacement landing
/// mid-question never mixes the ranges of one table with the names of another.
///
/// IPv4 and IPv6 are kept apart. Sixteen bytes for an address that needs four
/// triples what the table costs, and the two families never fall in one another's
/// ranges, so a search that mixed them would only have to tell them apart again.
/// </remarks>
internal sealed class IpCountryLookup : IIpCountryLookup
{
    private readonly ILogger<IpCountryLookup> _logger;
    private readonly string _path;

    /// <summary>
    /// Taken by the first read of the file and by every replacement, so a slow
    /// first read cannot land over a table that was replaced while it ran.
    /// Questions never take it once a table is held.
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
        if (string.IsNullOrWhiteSpace(ipAddress)) return null;
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

    /// <summary>The range that holds this address, if the table has one.</summary>
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

    /// <summary>
    /// Makes this table the one every later question is answered from.
    /// </summary>
    /// <remarks>
    /// One reference swapped: a question already under way finishes on the table
    /// it took, the next one takes this.
    /// </remarks>
    internal void Use(Table table)
    {
        lock (_gate)
        {
            Volatile.Write(ref _table, table);
        }
    }

    /// <summary>The table in use, read from the configured file the first time.</summary>
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

    /// <summary>
    /// Reads the configured table, or hands back an empty one and says why.
    /// </summary>
    /// <remarks>
    /// A missing file is not a failure to start: this decides one line of one
    /// letter, and refusing to boot over it would take the whole site down for a
    /// stale download. It is worth a warning, because a silently absent country
    /// looks exactly like a country nobody could determine.
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
    /// <param name="path">File to read.</param>
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
    /// Not written out here: two hundred and forty names by hand is two hundred
    /// and forty chances to misspell one, and the list moves when a country does.
    ///
    /// Not RegionInfo either, which is the obvious way and the wrong one. Its
    /// DisplayName hands back the name a country calls itself — "Deutschland",
    /// "Việt Nam", "საქართველო" — whatever the interface culture is, and a
    /// reader asked to recognise where they signed in from cannot read half of
    /// them. The name of a culture can: "немецкий (Германия)" under ru-RU, where
    /// the country stands in the parentheses, and what is in front of it is the
    /// language, which this does not want. Several cultures name the same
    /// country and they all name it the same way, so the first wins.
    ///
    /// A code no culture names is left out of the map, and the letter then
    /// prints no country rather than printing "VN" at the reader. A runtime
    /// built without culture data leaves the map empty, which is the same
    /// outcome for every address.
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
    /// "немецкий (Германия)" is the plain shape. A culture that also names a
    /// script puts it first — "сербский (латиница, Сербия)" — so the country is
    /// what follows the last comma inside the brackets.
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

    private static uint ToV4(IPAddress address)
    {
        Span<byte> bytes = stackalloc byte[4];
        address.TryWriteBytes(bytes, out _);
        return BitConverter.IsLittleEndian
            ? BinaryPrimitivesReverse(bytes)
            : BitConverter.ToUInt32(bytes);
    }

    /// <summary>Big-endian, so that numeric order is address order.</summary>
    private static uint BinaryPrimitivesReverse(ReadOnlySpan<byte> bytes) =>
        ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];

    private static UInt128 ToV6(IPAddress address)
    {
        Span<byte> bytes = stackalloc byte[16];
        address.TryWriteBytes(bytes, out _);
        UInt128 value = 0;
        foreach (var part in bytes) value = (value << 8) | part;
        return value;
    }

    internal readonly record struct Range<T>(T First, T Last, string Code) where T : IComparable<T>;

    /// <summary>A whole table: what one question is answered from.</summary>
    internal sealed record Table(
        Range<uint>[] V4,
        Range<UInt128>[] V6,
        Dictionary<string, string> Names)
    {
        public static Table Empty { get; } = new([], [], new Dictionary<string, string>(StringComparer.Ordinal));

        public int RangeCount => V4.Length + V6.Length;
    }
}
