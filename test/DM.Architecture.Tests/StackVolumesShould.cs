using AwesomeAssertions;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

/// <remarks>
/// A directory in the container layer is gone with the container, and the
/// update mechanism recreates containers on its own schedule.
/// </remarks>
public class StackVolumesShould
{
    /// <remarks>
    /// The API downloads the table itself as the image user, and a named volume takes the owner of the
    /// directory the image creates at that path, so the image hands that directory to its user.
    /// </remarks>
    [Fact]
    public void GiveTheApiAWritableVolumeForTheCountryTableOnTheStand()
    {
        var preview = Read(PreviewCompose);
        var dockerfile = Read("app.Dockerfile");

        preview.Should().Contain(
            "DM_GeoLocationConfiguration__CountryDatabasePath: '/data/geo/ip-country.csv'",
            "the table is read from and written to the path the setting names, inside the container");
        preview.Should().Contain("- geodata:/data/geo",
            "a named volume keeps the downloaded table across a recreated container");
        preview.Should().NotContain("/data/geo:ro",
            "the API writes each new copy of the table there itself");
        preview.Should().MatchRegex(@"(?m)^volumes:\s*\n(\s+\w+:\s*\n)*\s+geodata:",
            "a named volume the file does not declare stops compose before any container starts");
        dockerfile.Should().Contain("chown dmuser /data/geo",
            "a new volume takes the owner of the directory it is mounted over, and a root-owned " +
            "one leaves the download nowhere to be written");
    }
}
