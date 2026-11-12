using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

// A workflow is read as text: nothing runs one under test.
public class ContinuousIntegrationShould
{
    // The vulnerability gate reads the assets file, so it needs a restore on a clean checkout.
    [Fact]
    public void RestoreBeforeListingVulnerablePackages()
    {
        // Commands only. The header comment names both of them, so a search over
        // the whole text finds the prose rather than the script.
        var commands = File.ReadAllLines(
                Path.Combine(DockerDirectory, "..", "scripts", "check-vulnerable-packages.sh"))
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToList();

        var restore = commands.FindIndex(l => l.Contains("dotnet restore", StringComparison.Ordinal));
        var list = commands.FindIndex(l => l.Contains("dotnet list", StringComparison.Ordinal));

        restore.Should().BeGreaterThan(-1, "dotnet list package reads the assets file");
        list.Should().BeGreaterThan(-1, "the script must still be the one running the check");
        restore.Should().BeLessThan(list, "restoring after the check helps nobody");
    }

    /// <remarks>
    /// The development ports are allowed only in Development, and the end-to-end job runs
    /// Production, so its overlay hands the API the port the suite serves from.
    /// </remarks>
    [Fact]
    public void ServeTheEndToEndSuiteFromAnOriginTheApiAnswersTo()
    {
        var fixtures = File.ReadAllText(Path.Combine(
            RepositoryLayout.Root, "src", "DM.Web.Client", "e2e", "fixtures", "auth.ts"));
        var workflow = File.ReadAllText(
            Path.Combine(RepositoryLayout.Root, ".github", "workflows", "dotnet.yml"));

        var declared = Regex.Match(fixtures, @"PREVIEW_PORT\s*=\s*(\d+)");
        declared.Success.Should().BeTrue(
            "the suite declares the port it serves the bundle from, and a walk that cannot " +
            "find it checks nothing");

        var port = declared.Groups[1].Value;

        // Past the heredoc opener: its marker is the word itself.
        var overlay = Between(workflow,
            "cat > docker/docker-compose.e2e.yml <<'YAML'", "\n        YAML");

        overlay.Should().Contain($"AdditionalOrigins__0: \"http://localhost:{port}\"",
            $"the bundle is served from port {port} and the API adds the development ports " +
            "only under Development, which this job is not - so an origin it does not answer " +
            "to means CORS drops every response and the origin check refuses every write");
    }

    /// <remarks>
    /// The example environment ends with Development. Asked of every job that starts the stack, not
    /// of the file: one workflow starts it twice.
    /// </remarks>
    [Theory]
    [InlineData("dotnet.yml")]
    [InlineData("security.yml")]
    public void RunTheGatesAgainstTheEnvironmentThatShips(string workflow)
    {
        var jobs = WorkflowJobs.Read(workflow)
            .Select(job => (Name: job.Name, Body: string.Join('\n', job.Lines)))
            .ToList();

        jobs.Should().NotBeEmpty($"{workflow} declares jobs");

        var starters = jobs
            .Where(job => job.Body.Contains("docker compose up", StringComparison.Ordinal))
            .ToList();

        starters.Should().NotBeEmpty(
            $"{workflow} is one of the workflows that start the stack");
        foreach (var (name, body) in starters)
        {
            body.Should().Contain("ASPNETCORE_ENVIRONMENT: Production",
                $"job {name} of {workflow} exercises Development, which proves nothing " +
                "about what gets deployed");
        }
    }

    // A coverage file uploaded with no threshold guarantees nothing.
    [Fact]
    public void CheckTheCoverageTheBuildCollects()
    {
        var workflow = File.ReadAllText(
            Path.Combine(RepositoryLayout.Root, ".github", "workflows", "dotnet.yml"));

        File.Exists(Path.Combine(RepositoryLayout.Root, "scripts", "check-coverage.sh"))
            .Should().BeTrue("the threshold lives beside the reason for it, in the script");

        var collection = workflow.IndexOf("--coverage", StringComparison.Ordinal);
        var check = workflow.IndexOf("check-coverage.sh", StringComparison.Ordinal);

        check.Should().BeGreaterThan(-1, "a number nobody reads is a report, not a gate");
        collection.Should().BeGreaterThan(-1, "the run still collects the coverage");
        collection.Should().BeLessThan(check, "the check reads what the run collected");
    }
}
