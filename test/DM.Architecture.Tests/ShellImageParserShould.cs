using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

/// <remarks>
/// The pin rule and the stack document read the images of the scripts through this parser, so
/// a form it skips is an image that is neither pinned nor named, with both of them green.
/// </remarks>
public sealed class ShellImageParserShould : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("dm-shell-images-");

    public void Dispose() => _directory.Delete(recursive: true);

    [Fact]
    public void FindARunInsideACommandSubstitution() =>
        ImagesOf("out=\"$(docker run --rm alpine:latest id)\"")
            .Should().Equal("alpine:latest");

    [Fact]
    public void CollapseASubstitutionInTheArguments() =>
        ImagesOf("docker run --rm -v \"$(host \"$ROOT/a b\"):/a:ro\" alpine:3.20 true")
            .Should().Equal("alpine:3.20");

    [Theory]
    [InlineData("IMAGE=\"${IMAGE:-alpine:latest}\"")]
    [InlineData("local IMAGE=alpine:latest")]
    [InlineData("export IMAGE=\"alpine:latest\"")]
    [InlineData("readonly IMAGE='alpine:latest'")]
    public void ResolveTheAssignmentOfAVariable(string assignment) =>
        ImagesOf(assignment, "docker run --rm \"$IMAGE\" id")
            .Should().Equal("alpine:latest");

    [Fact]
    public void KeepAComputedImageTheVariableItIs() =>
        ImagesOf("IMAGE=\"$(grep -oE 'prom/prometheus:[0-9.]+' docker-compose.yml)\"", "docker run --rm \"$IMAGE\" id")
            .Should().Equal("$IMAGE");

    [Fact]
    public void RefuseAVariableWithNothingToReadItFrom()
    {
        var parse = () => ImagesOf("docker run --rm \"$IMAGE\" id");

        parse.Should().Throw<InvalidOperationException>().WithMessage("*$IMAGE*");
    }

    [Fact]
    public void RefuseARunWithNoImage()
    {
        var parse = () => ImagesOf("run() { docker run --rm \"$@\"; }", "run alpine:latest id");

        parse.Should().Throw<InvalidOperationException>().WithMessage("*no image*");
    }

    private string[] ImagesOf(params string[] lines)
    {
        File.WriteAllText(Path.Combine(_directory.FullName, "probe.sh"), string.Join('\n', lines) + '\n');
        return ImagesRunByShellScripts(_directory.FullName).Select(found => found.Image).ToArray();
    }
}
