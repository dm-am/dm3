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
        // Every start on a server goes through the release script.
        var entry = File.ReadAllText(Path.Combine(DockerDirectory, "scripts", "release-entry.sh"));
        var compose = Read(BaseCompose) + Read(PreviewCompose);

        var starts = entry.Split('\n')
            .Where(line => !line.TrimStart().StartsWith('#')
                           && line.Contains("up -d", StringComparison.Ordinal)).ToList();

        starts.Should().NotBeEmpty().And.OnlyContain(
            line => line.Contains("--no-build", StringComparison.Ordinal)
                    && line.Contains("--pull never", StringComparison.Ordinal),
            "the deployment runs the images the release pulled by digest, and a build or a pull " +
            "here would replace them with others under the same name");

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
                "commit gives a different runtime a month later");
        }
    }

    /// <remarks>
    /// A release names each application image by digest through one variable, and without it the
    /// stand and the point of presence keep the IMAGE_TAG form. The name of the variable follows
    /// the image, so a service sharing an image (the migration and the API) shares its variable.
    /// </remarks>
    [Fact]
    public void NameTheApplicationImagesByAVariableTheReleaseSets()
    {
        var references = (Read(BaseCompose) + "\n" + Read(PreviewCompose))
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("image:", StringComparison.Ordinal)
                           && line.Contains("ghcr.io/dm-am/", StringComparison.Ordinal))
            .Select(line => line[6..].Trim().Trim('\'', '"'))
            .ToList();

        references.Should().HaveCountGreaterThanOrEqualTo(5,
            "the migration, the API, both workers and the frontend run published images");

        var images = references.Select(reference =>
        {
            var match = Regex.Match(reference,
                @"^\$\{(?<variable>DM_IMAGE_\w+):-ghcr\.io/dm-am/dm-(?<image>[a-z-]+):\$\{IMAGE_TAG:-latest\}\}$");
            match.Success.Should().BeTrue(
                $"{reference} has to take the release's digest when it is set and IMAGE_TAG otherwise");
            match.Groups["variable"].Value.Should().Be(
                "DM_IMAGE_" + match.Groups["image"].Value.ToUpperInvariant().Replace('-', '_'),
                $"{reference}: one image, one variable, or a release sets one service and not the other");
            return match.Groups["image"].Value;
        }).Distinct();

        images.Should().BeEquivalentTo(new[] { "api", "front", "worker-mail", "worker-notification" },
            "the release carries the four application images and nothing else");
    }

    /// <remarks>
    /// A rewritten tag would change the edge under a release that did not ask for it, and the edge
    /// changes only by maintenance. Dependabot moves the pin, and the parse check reads the
    /// configuration with the very image that serves it.
    /// </remarks>
    [Fact]
    public void PinTheEdgeByTagAndDigest()
    {
        var image = ServiceBlock(Read(PreviewCompose), "nginx")
            .Split('\n')
            .Select(line => line.Trim())
            .Single(line => line.StartsWith("image:", StringComparison.Ordinal))[6..]
            .Trim().Trim('\'', '"');

        image.Should().MatchRegex(@"^nginx:[\w.-]+@sha256:[0-9a-f]{64}$",
            "a tag alone can be rewritten under the running edge");

        var check = File.ReadAllText(Path.Combine(
            RepositoryLayout.Root, "scripts", "deployment", "check-nginx-configuration.sh"));
        Regex.Match(check, @"^IMAGE=""(?<image>[^""]+)""", RegexOptions.Multiline).Groups["image"].Value
            .Should().Be(image, "a configuration parsed by another nginx proves nothing about the edge");

        var dependabot = File.ReadAllText(Path.Combine(RepositoryLayout.Root, ".github", "dependabot.yml"));
        dependabot.Should().MatchRegex(@"package-ecosystem: docker-compose\s*\n\s*directory: /docker\b",
            "the docker entry reads Dockerfiles, and a pin nobody moves never receives a patch");
    }

    /// <remarks>The release reads which commit an image was built from out of its revision label.</remarks>
    [Theory]
    [InlineData(BaseCompose, "migration")]
    [InlineData(BaseCompose, "dmapi")]
    [InlineData(BaseCompose, "dm-mail-worker")]
    [InlineData(BaseCompose, "dm-notification-worker")]
    [InlineData(PreviewCompose, "dmfront")]
    public void LabelEveryDeliveredImageWithItsRevision(string file, string service)
    {
        ServiceBlock(Read(file), service).Should().Contain("SOURCE_REVISION: ${SOURCE_REVISION:-}",
            $"{service} is built here, and both Dockerfiles turn the argument into the revision label");
    }
}
