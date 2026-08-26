using System.Linq;
using DM.Domain.Core.Configuration;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests.Configuration;

/// <summary>
/// The origins a browser may call from are the addresses the site answers on.
/// </summary>
/// <remarks>
/// They used to be a second list beside the first, and no path of any deployment
/// ever wrote the public address of a stand into it: not the installer, not the
/// environment file, not the guide. The first request to the first stand would
/// have been refused at the door by the origin check — against a list sitting two
/// lines away from the address it refused.
///
/// Derived, the question does not arise: declaring an address is the only thing
/// anybody has to do, and it is the thing a deployment cannot avoid doing.
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
    /// A browser sends the origin in its canonical form. An address written down by
    /// hand has a trailing slash, a spelt-out default port or a capital in the host
    /// as often as not, and each of those made the derived list disagree with the
    /// header while looking identical to a reader.
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
