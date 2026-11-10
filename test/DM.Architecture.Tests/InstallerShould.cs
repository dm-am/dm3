using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
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

    // Coherence of the guide, the usage comment and the default, not the choice of branch.
    [Fact]
    public void InstallFromTheRefTheDocumentedCommandDownloads()
    {
        var installer = File.ReadAllText(Path.Combine(DockerDirectory, "setup-server.sh"));

        Regex.IsMatch(installer, @"DM_REF=""\$\{DM_REF:-").Should().BeTrue(
            "the installer picks what it clones, and it has to name a default for a " +
            "command that passes nothing");

        Regex.IsMatch(installer, @"git clone --branch ""\$DM_REF""").Should().BeTrue(
            "the clone takes the ref the caller asked for, not a second name for it");

        // README.md is the first page a reader meets.
        var sources = Directory
            .GetFiles(Path.Combine(RepositoryLayout.Root, "docs"), "*.md", SearchOption.AllDirectories)
            .Append(Path.Combine(RepositoryLayout.Root, "README.md"))
            .Append(Path.Combine(DockerDirectory, "setup-server.sh"));

        var refs = sources
            .SelectMany(path => Regex.Matches(
                File.ReadAllText(path),
                @"raw\.githubusercontent\.com/[\w.\-]+/[\w.\-]+/([^/]+)/docker/setup-server\.sh")
                .Select(match => (File: Path.GetFileName(path), Ref: match.Groups[1].Value)))
            .ToList();

        refs.Should().NotBeEmpty(
            "the automatic installation is documented as a URL with the ref in its path");

        // The ref is written once and read twice.
        foreach (var (file, reference) in refs)
        {
            reference.Should().Be("$DM_REF",
                $"{file} spells the ref out instead of passing the one the operator chose, " +
                "so the script downloaded and the tree cloned drift apart");
        }
    }

    // The same invariant as the CI step, off the runner: a reboot must not bring up a bare API.
    [Fact]
    public void DeployTheSameFilesAndProfilesFromTheUnitAndTheInstaller()
    {
        var installer = ComposeSelectors(
            File.ReadAllText(Path.Combine(DockerDirectory, "setup-server.sh")));
        var unit = ComposeSelectors(
            File.ReadAllText(Path.Combine(DockerDirectory, "dm3.service")));

        installer.Should().Contain(BaseCompose).And.Contain(PreviewCompose,
            "the site lives in the overlay, and a command naming the base file alone starts " +
            "the API without it");
        installer.Should().Contain("production",
            "watchtower sits behind that profile, and a deployment without it stops updating");
        unit.Should().BeEquivalentTo(installer,
            "a reboot has to bring up the set the installer deployed");
    }

    /// <summary>
    /// The updater the guides call the update mechanism has to be started by the
    /// commands that deploy.
    /// </summary>
    /// <remarks>watchtower sits behind a profile the deploying commands have to enable.</remarks>
    [Fact]
    public void StartTheUpdaterTheDeploymentUpdatesItselfWith()
    {
        var compose = Read(BaseCompose);
        var installer = File.ReadAllText(Path.Combine(DockerDirectory, "setup-server.sh"));
        var unit = File.ReadAllText(Path.Combine(DockerDirectory, "dm3.service"));

        ServiceBlock(compose, "watchtower").Should().Contain("production",
            "the profile the deployment enables is the one the service has to declare");
        installer.Should().Contain("--profile production",
            "a service behind a profile nobody enables never starts");
        unit.Should().Contain("--profile production",
            "a reboot must bring up the set the installer deployed");

        foreach (var service in new[] { "dmapi", "dm-mail-worker", "dm-notification-worker" })
        {
            ServiceBlock(compose, service).Should().Contain("watchtower.enable=true",
                $"{service} runs a published image, and only labelled containers are updated");
        }

        ServiceBlock(Read(PreviewCompose), "dmfront").Should().Contain("watchtower.enable=true",
            "the SPA image is published by the same run and has to move with it");
    }

    /// <remarks>
    /// The json-file driver keeps every line forever. Given to the daemon, which also covers the one-off
    /// containers of the scripts.
    /// </remarks>
    [Fact]
    public void CapTheLogOfEveryContainerOnTheServer()
    {
        var installer = File.ReadAllText(Path.Combine(DockerDirectory, "setup-server.sh"));

        var policy = installer.IndexOf("/etc/docker/daemon.json", StringComparison.Ordinal);
        var start = installer.IndexOf("docker compose", StringComparison.Ordinal);

        policy.Should().BeGreaterThan(-1,
            "the default driver keeps every line of every container until the disk is full");
        installer.Should().Contain("max-size", "a driver with no size limit never rotates");
        installer.Should().Contain("max-file", "one rotated file is one file that keeps growing");

        start.Should().BeGreaterThan(-1, "the installer is still the thing that starts the stack");
        policy.Should().BeLessThan(start,
            "log options apply to containers created after they are set");
    }

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
