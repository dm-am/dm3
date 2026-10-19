using System.Linq;
using DM.Domain.Core.Configuration;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests.Configuration;

/// <summary>
/// The origins a browser may call from are the addresses the site answers on.
/// </summary>
/// <remarks>
/// Derived, so declaring an address is the only thing a deployment has to do.
/// </remarks>
public class SiteAddressConfigurationShould
{
    private static SiteAddressConfiguration Site => new() { PublicUrl = "https://dm.am" };

    [Fact]
    public void AllowEveryAddressTheSiteAnswersOn()
    {
        Site.BrowserOrigins().Should().BeEquivalentTo(
            DM.Domain.Core.Site.SiteAddresses.Hosts.Select(host => $"https://{host}"),
            "the addresses of the site are one declaration, and the second door is one of them");
    }

    [Fact]
    public void CountTheSameAddressOnce()
    {
        Site.BrowserOrigins().Should().OnlyHaveUniqueItems(
            "the public address is normally one of the declared ones, and a list read by " +
            "two middlewares should not depend on how many times it is written down");
    }

    [Fact]
    public void CarryTheOriginsThatAreNotAddressesOfTheSite()
    {
        var site = Site;
        site.AdditionalOrigins = ["http://localhost:5173"];

        site.BrowserOrigins().Should().Contain("http://localhost:5173",
            "the development server of the client is not an address of the site and still calls it");
    }

    /// <summary>
    /// Both sides of the comparison go through one normaliser.
    /// </summary>
    /// <remarks>
    /// A browser sends the canonical origin, an address written by hand often is not.
    /// </remarks>
    [Theory]
    [InlineData("https://DM.am/", "https://dm.am")]
    [InlineData("https://dm.am:443", "https://dm.am")]
    [InlineData("http://localhost:5173/app", "http://localhost:5173")]
    [InlineData("dm.am", null)]
    [InlineData("ftp://dm.am", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ReduceAnAddressToItsOrigin(string? address, string? expected)
    {
        SiteAddressConfiguration.OriginOf(address).Should().Be(expected);
    }

    [Fact]
    public void DropAnAddressNobodyCanParse()
    {
        var site = Site;
        site.AdditionalOrigins = ["htps://dm.am"];

        site.BrowserOrigins().Should().NotContain(origin => origin.Contains("htps"),
            "a typo must not reach the two middlewares that compare against this list");
    }
}
