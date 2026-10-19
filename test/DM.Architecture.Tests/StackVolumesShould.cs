using AwesomeAssertions;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

/// <summary>
/// The state of the stack that must outlive a container is on a named volume.
/// </summary>
/// <remarks>
/// A directory in the container layer is gone with the container, and the
/// update mechanism recreates containers on its own schedule.
/// </remarks>
public class StackVolumesShould
{
    /// <summary>
    /// The stand gives the API a place of its own for the country table, at the
    /// path the setting names, that the API can write and that outlives the
    /// container.
    /// </summary>
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

    /// <summary>
    /// The broker holds work nothing else records, so its state needs a volume like
    /// every other store.
    /// </summary>
    /// <remarks>
    /// The mail queue and its dead letters are the only record of a letter owed or failed. Mnesia keeps
    /// its data under a directory named after the node, so the hostname is fixed as well.
    /// </remarks>
    [Fact]
    public void KeepTheBrokerStateInANamedVolume()
    {
        var compose = Read(BaseCompose);

        compose.Should().Contain("rmqdata:/var/lib/rabbitmq",
            "a durable queue whose directory lives in the container layer is durable " +
            "only until the container is recreated");
        compose.Should().Contain("hostname: 'dm-rmq'",
            "mnesia stores its data under a directory named after the node, so a " +
            "generated hostname makes the volume unreadable to the next container");
    }
}
