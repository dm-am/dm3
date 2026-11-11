using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

// Nothing executes the installer under test, so its lines are asserted as text.
public class InstallerShould
{
    // The compose default is Mailpit on loopback; a contour that delivers nowhere is worse than
    // none.
    [Fact]
    public void RefuseToInstallAServerWithNowhereToSendMail()
    {
        var generator = File.ReadAllText(
            Path.Combine(DockerDirectory, "scripts", "init-env.sh"));

        var check = generator.IndexOf("is_empty MAIL_HOST", StringComparison.Ordinal);
        check.Should().BeGreaterThan(-1,
            "the relay address is the one answer a server cannot be given by a default");

        var tail = generator[check..];
        tail.Should().Contain("exit 1",
            "a warning on stderr followed by exit 0 is a clean run to the installer " +
            "calling this, which is how the environment stayed Development once already");
    }

    /// <summary>
    /// The server is installed from a checkout of a verified tag, and the layout from the signed tag
    /// itself, never from a ref that moves.
    /// </summary>
    /// <remarks>
    /// The installer used to be piped from a ref and clone a working tree. Now the host part hands over
    /// to the layout part, which refuses to start without the signer's key and the tag.
    /// </remarks>
    [Fact]
    public void InstallTheLayoutFromTheSignedTagAlone()
    {
        var installer = ShellCode(Path.Combine(DockerDirectory, "setup-server.sh"));
        var layout = ShellCode(Path.Combine(DockerDirectory, "scripts", "install-release.sh"));

        installer.Should().NotContain("git clone",
            "a clone is a tree that moves with its branch; the layout is exported from the signed commit");
        installer.Should().NotContain("DM_REF",
            "a ref passed on the command line is text anyone can choose; the tag is verified against the pinned key");
        installer.Should().Contain("install-release.sh",
            "the host part prepares the machine and hands the layout to the part that pins the key");

        layout.Should().Contain("--signers").And.Contain("--tag",
            "without the signer's key the server would accept any tag shaped like a release");
        layout.Should().Contain("maintain \"$TAG\"",
            "the first release is installed by the same verified path every later maintenance takes");
    }

    // The same invariant as the CI step, off the runner: a reboot must not bring up a bare API.
    [Fact]
    public void BootTheReleaseTheLayoutRunsFromItsOwnFiles()
    {
        var unit = File.ReadAllText(Path.Combine(DockerDirectory, "dm3.service"));
        var entry = File.ReadAllText(Path.Combine(DockerDirectory, "scripts", "release-entry.sh"));

        unit.Should().Contain("ExecStart=/usr/local/sbin/dm3-release boot",
            "a reboot starts what rel/current names and puts the edge back on the primaries");
        unit.Should().Contain("ExecStop=/usr/local/sbin/dm3-release stop",
            "the stop goes through the same compose command line as the start");
        unit.Should().NotContain("docker compose",
            "a compose line of its own in the unit is a second definition of the stand that drifts");

        ComposeSelectors(entry).Should().BeEquivalentTo(new[] { BaseCompose, PreviewCompose },
            "the site lives in the overlay, and the release's command line names both files and no profile");
    }

    /// <summary>
    /// No container holds the docker socket.
    /// </summary>
    /// <remarks>
    /// Which release runs changes only through the release script, from a tag the pinned key signed. A
    /// container with the socket is root on the host and could replace containers on its own schedule.
    /// </remarks>
    [Fact]
    public void HoldTheDockerSocketInNoContainer()
    {
        foreach (var file in new[] { BaseCompose, PreviewCompose, PopCompose })
        {
            Read(file).Should().NotContain("docker.sock",
                $"{file} would hand a container the host's docker, beside the signed releases");
        }
    }

    /// <remarks>
    /// The json-file driver keeps every line forever. Given to the daemon, which also covers the one-off
    /// containers of the scripts.
    /// </remarks>
    [Fact]
    public void CapTheLogOfEveryContainerOnTheServer()
    {
        var installer = ShellCode(Path.Combine(DockerDirectory, "setup-server.sh"));

        var policy = installer.IndexOf("/etc/docker/daemon.json", StringComparison.Ordinal);
        var layout = installer.IndexOf("install-release.sh", StringComparison.Ordinal);

        policy.Should().BeGreaterThan(-1,
            "the default driver keeps every line of every container until the disk is full");
        installer.Should().Contain("max-size", "a driver with no size limit never rotates");
        installer.Should().Contain("max-file", "one rotated file is one file that keeps growing");

        layout.Should().BeGreaterThan(-1, "the layout part, which starts the stack, is still called from here");
        policy.Should().BeLessThan(layout,
            "log options apply to containers created after they are set");
    }

    /// <summary>A shell file without its comment lines: the scripts quote the commands they replaced.</summary>
    private static string ShellCode(string path) => string.Join('\n', File.ReadAllLines(path)
        .Where(line => !line.TrimStart().StartsWith('#')));

    private static List<string> ComposeSelectors(string text)
    {
        var selectors = text.Split('\n')
            .Where(line => line.Contains("docker compose", StringComparison.Ordinal))
            .SelectMany(line => Regex.Matches(line, @"(?:-f|--file)\s+(\S+\.yml)|--profile\s+(\S+)"))
            .Select(match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)
            .Select(Path.GetFileName)
            .Distinct()
            .ToList();

        selectors.Should().NotBeEmpty(
            "an extraction that finds nothing compares equal to another that finds nothing");
        return selectors!;
    }
}
