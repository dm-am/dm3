using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

// An image reference lives in the compose files, the Dockerfiles and the shell scripts.
public class ImageDeliveryShould
{
    // A local build takes the registry's name, so later pulls never happen.
    [Fact]
    public void PullThePublishedImagesInsteadOfBuildingOnTheServer()
    {
        var installer = File.ReadAllText(Path.Combine(DockerDirectory, "setup-server.sh"));
        var compose = Read(BaseCompose) + Read(PreviewCompose);

        var starts = installer.Split('\n')
            .Where(line => line.Contains("docker compose", StringComparison.Ordinal)
                           && line.Contains("up -d", StringComparison.Ordinal)).ToList();

        starts.Should().NotBeEmpty().And.NotContain(line => line.Contains("--build", StringComparison.Ordinal),
            "the deployment pulls the published image, and building it here replaces it " +
            "with a local one under the same tag");

        var localTags = compose.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("image:", StringComparison.Ordinal)
                           && line.Contains(":local", StringComparison.Ordinal))
            .ToList();

        foreach (var tag in localTags)
        {
            tag.Should().Contain("dm-seeder",
                "a local tag cannot be pulled, so every service holding one is built on " +
                "the server; the seeder is the exception, the tools profile never starts " +
                "it there");
        }
    }

    // An image nobody pulls is not delivery.
    [Fact]
    public void PullEveryImageTheDeliveryWorkflowPublishes()
    {
        var workflow = File.ReadAllText(
            Path.Combine(DockerDirectory, "..", ".github", "workflows", "dotnet.yml"));
        var compose = Read(BaseCompose) + Read(PreviewCompose);

        var published = Regex.Matches(workflow, @"image_suffix:\s*(\S+)")
            .Select(match => match.Groups[1].Value)
            .Concat(Regex.Matches(workflow, @"IMAGE_PREFIX\s*\}\}-([a-z-]+)")
                .Select(match => match.Groups[1].Value))
            .Distinct()
            .ToList();

        published.Should().HaveCountGreaterThan(1, "the parser must find the published images");
        foreach (var image in published)
        {
            compose.Should().Contain($"/dm-{image}:",
                $"dm-{image} is pushed on every merge, so a deployment has to name it");
        }
    }

    /// <remarks>
    /// A reference with no tag resolves to whatever the registry holds that day; the store client and
    /// server are pinned together by digest. "latest" is refused by name, and the images published here
    /// keep IMAGE_TAG a variable.
    /// </remarks>
    [Fact]
    public void PinEveryImageTheDeploymentRuns()
    {
        var composeImages = (Read(BaseCompose) + "\n" + Read(PreviewCompose))
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("image:", StringComparison.Ordinal))
            .Select(line => line[6..].Trim().Trim('\'', '"'));

        var dockerfileImages = new[]
            {
                Path.Combine(DockerDirectory, "app.Dockerfile"),
                Path.Combine(RepositoryLayout.Root, "src", "DM.Web.Client", "Dockerfile"),
            }
            .SelectMany(path => File.ReadAllLines(path))
            .Where(line => line.StartsWith("FROM ", StringComparison.Ordinal))
            .Select(line => line[5..].Split(' ')[0]);

        var shellImages = ImagesRunByShellScripts(DockerDirectory).Select(found => found.Image).ToList();

        shellImages.Should().HaveCountGreaterThan(1,
            "the parser must find the images the scripts run, in both spellings: the bare " +
            "command and the installer's array that carries sudo");

        var references = composeImages
            .Concat(dockerfileImages)
            .Concat(shellImages)
            .ToList();

        references.Should().HaveCountGreaterThan(10, "the parser must find the image references");

        var published = references.Where(reference => reference.Contains("${", StringComparison.Ordinal)).ToList();
        var external = references.Except(published).ToList();

        published.Should().NotBeEmpty("the stack runs the images this repository builds");
        external.Should().HaveCountGreaterThan(10, "the parser must find the third-party references");

        foreach (var reference in published)
        {
            reference.Should().Contain("${IMAGE_TAG",
                $"{reference} is built here, and which build a server runs is the deployment's " +
                "choice through IMAGE_TAG; a tag written into the file takes that choice away");
        }

        foreach (var reference in external)
        {
            var name = reference.Split('/')[^1];
            var separator = name.LastIndexOf(':');

            separator.Should().BeGreaterThan(0,
                $"{reference} resolves to whatever the registry holds on the day it is pulled");
            name[(separator + 1)..].Should().NotBeEquivalentTo("latest",
                $"{reference} follows the registry rather than this repository, so the same " +
                "commit gives a different runtime a month later - and watchtower, the one that " +
                "would decide it, mounts /var/run/docker.sock");
        }
    }
}
