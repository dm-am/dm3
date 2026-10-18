using System;
using System.IO;
using AwesomeAssertions;
using DM.Domain.Core.Configuration;
using DM.Infrastructure.Core.Geo;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Geo;

/// <summary>
/// The country of an address, read out of a local table of ranges.
/// </summary>
/// <remarks>
/// One letter prints this, the warning about a login from a device the account
/// has not seen. Everything here is about the answer being either right or
/// absent: a letter about security that names the wrong country costs a password
/// reset and a ticket, and one that names none costs a line.
/// </remarks>
public class IpCountryLookupShould : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"dm3-country-{Guid.NewGuid():N}.csv");

    private IpCountryLookup Reading(params string[] lines)
    {
        File.WriteAllLines(_path, lines);
        return Configured(_path);
    }

    private static IpCountryLookup Configured(string path) => new(
        Options.Create(new GeoLocationConfiguration { CountryDatabasePath = path }),
        NullLogger<IpCountryLookup>.Instance);

    [Fact]
    public void NameTheCountryOfAnAddressInsideARange()
    {
        var lookup = Reading(
            "1.0.0.0,1.0.0.255,AU",
            "203.0.113.0,203.0.113.255,RU",
            "212.0.0.0,212.255.255.255,DE");

        lookup.Find("203.0.113.42").Should().Be("Россия");
    }

    /// <summary>The ends of a range belong to it.</summary>
    [Theory]
    [InlineData("203.0.113.0")]
    [InlineData("203.0.113.255")]
    public void CountTheEndsOfARangeAsInsideIt(string address)
    {
        var lookup = Reading("203.0.113.0,203.0.113.255,RU");

        lookup.Find(address).Should().Be("Россия");
    }

    [Fact]
    public void NameNothingForAnAddressNoRangeHolds()
    {
        var lookup = Reading("203.0.113.0,203.0.113.255,RU");

        lookup.Find("198.51.100.7").Should().BeNull();
    }

    [Fact]
    public void ReadTheSixthVersionOfAnAddressToo()
    {
        var lookup = Reading(
            "203.0.113.0,203.0.113.255,RU",
            "2001:db8::,2001:db8:ffff:ffff:ffff:ffff:ffff:ffff,DE");

        lookup.Find("2001:db8::1").Should().Be("Германия");
    }

    /// <summary>
    /// The two families never fall inside one another's ranges, and a table
    /// holding both must not let one answer for the other.
    /// </summary>
    [Fact]
    public void KeepTheTwoFamiliesApart()
    {
        var lookup = Reading(
            "0.0.0.0,255.255.255.255,AU",
            "2001:db8::,2001:db8::ffff,DE");

        lookup.Find("2001:db8::1").Should().Be("Германия");
        lookup.Find("8.8.8.8").Should().Be("Австралия");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not an address")]
    [InlineData("999.999.999.999")]
    public void NameNothingForInputThatIsNotAnAddress(string? input)
    {
        var lookup = Reading("203.0.113.0,203.0.113.255,RU");

        lookup.Find(input).Should().BeNull();
    }

    /// <summary>
    /// A code the framework has no name for is left out: the letter asks the
    /// reader to recognise a country, and two letters of a code are not one.
    /// </summary>
    [Fact]
    public void NameNothingForACodeItCannotSpell()
    {
        var lookup = Reading("203.0.113.0,203.0.113.255,ZZ");

        lookup.Find("203.0.113.42").Should().BeNull();
    }

    /// <summary>A publisher that quotes every field is read the same way.</summary>
    [Fact]
    public void ReadAQuotedTable()
    {
        var lookup = Reading("\"203.0.113.0\",\"203.0.113.255\",\"RU\"");

        lookup.Find("203.0.113.42").Should().Be("Россия");
    }

    /// <summary>
    /// The table the deployment downloads: user-country of sapics/ip-location-db.
    /// No header and no quotes, both families in one file, every IPv6 address in
    /// its compressed spelling, and no line at all over space no country holds.
    /// </summary>
    /// <remarks>
    /// The lines are copied from the publisher's file. The IPv6 ones are picked
    /// to make the compression land in different places: a run of zeros at the
    /// end, one between groups with more groups after it, and both ends of a
    /// range folded the same way.
    /// </remarks>
    [Fact]
    public void ReadTheTableTheDeploymentDownloads()
    {
        var lookup = Reading(
            "1.0.0.0,1.0.0.255,AU",
            "2.27.26.128,2.27.26.159,AQ",
            "5.3.0.0,5.3.255.255,RU",
            "2001:550:2:9::13c:0,2001:550:2:9::13c:ffff,JP",
            "2001:550:3::234,2001:550:3::237,DE",
            "2a02:6b8::,2a02:6bf:ffff:ffff:ffff:ffff:ffff:ffff,RU");

        lookup.Find("1.0.0.7").Should().Be("Австралия");
        lookup.Find("5.3.12.200").Should().Be("Россия");
        lookup.Find("2a02:6b8::feed").Should().Be("Россия");
        // The address arrives spelled out in full as well: the table and the
        // request need not agree on the spelling, only on the number.
        lookup.Find("2a02:06b8:0000:0000:0000:0000:0000:0001").Should().Be("Россия");
        lookup.Find("2001:550:2:9::13c:42").Should().Be("Япония");
        lookup.Find("2001:550:3::235").Should().Be("Германия");

        // The publisher leaves out the space nobody holds: no line, no country.
        lookup.Find("0.1.2.3").Should().BeNull();
        lookup.Find("::1").Should().BeNull();
        // Antarctica is in the table, and no culture names it: no name, no country.
        lookup.Find("2.27.26.130").Should().BeNull();
    }

    /// <summary>A broken line is dropped, and the lines around it still answer.</summary>
    [Fact]
    public void SkipALineItCannotRead()
    {
        var lookup = Reading(
            "# first,last,country",
            "1.0.0.0,1.0.0.255,AU",
            "garbage",
            "203.0.113.0,203.0.113.255,RU");

        lookup.Find("203.0.113.42").Should().Be("Россия");
        lookup.Find("1.0.0.7").Should().Be("Австралия");
    }

    /// <summary>
    /// No table is not a failure: this decides one line of one letter, and a
    /// stale download must not keep the site from starting.
    /// </summary>
    [Fact]
    public void NameNothingWhenTheTableIsMissing()
    {
        var lookup = Configured(Path.Combine(Path.GetTempPath(), $"dm3-absent-{Guid.NewGuid():N}.csv"));

        lookup.Find("203.0.113.42").Should().BeNull();
    }

    [Fact]
    public void NameNothingWhenNoTableIsConfigured()
    {
        var lookup = Configured(string.Empty);

        lookup.Find("203.0.113.42").Should().BeNull();
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        GC.SuppressFinalize(this);
    }
}
