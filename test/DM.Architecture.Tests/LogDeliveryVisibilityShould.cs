using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A log store that stops accepting has to be findable by something other than the
/// logs it is refusing.
/// </summary>
/// <remarks>
/// A sink reports nothing about itself by contract, and Loki drops silently after retrying. The
/// diagnostic stream, a scrape target on the store and a rule close that; the guide's queries are
/// asserted with them.
/// </remarks>
public class LogDeliveryVisibilityShould
{
    private static string Root => DM.Testing.RepositoryLayout.Root;

    private static string LoggingPath => Path.Combine(
        Root, "src", "DM.Infrastructure.Core", "Logging", "LoggingConfiguration.cs");

    private static string LoggingSource => SourceText.ReadCode(LoggingPath);

    private static string Guide => File.ReadAllText(
        Path.Combine(Root, "docs", "guides", "MONITORING.md"));

    /// <summary>Label keys the sink is told to attach, as the call spells them.</summary>
    private static readonly Regex DeclaredLabel = new(
        @"new LokiLabel\s*{\s*Key\s*=\s*""([\w-]+)""", RegexOptions.Compiled);

    /// <summary>A LogQL stream selector, as the guide writes one.</summary>
    private static readonly Regex StreamSelector = new(
        @"^\{([^}]*)\}", RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>One matcher inside a selector: the key and nothing else.</summary>
    private static readonly Regex SelectorKey = new(
        @"([\w-]+)\s*(?:=~|!~|!=|=)", RegexOptions.Compiled);

    /// <summary>
    /// The one label nobody declares: the sink adds it unless told otherwise.
    /// </summary>
    private const string LevelLabel = "level";

    /// <summary>
    /// Every line says which build wrote it.
    /// </summary>
    /// <remarks>Workflow, build and logger each hand the commit on, and none fails visibly alone.</remarks>
    [Fact]
    public void NameTheBuildEveryLineCameFrom()
    {
        var dockerfile = File.ReadAllText(Path.Combine(Root, "docker", "app.Dockerfile"));
        var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "dotnet.yml"));

        dockerfile.Should().Contain("ARG SOURCE_REVISION",
            "the build has to be able to receive the commit it is building");
        dockerfile.Should().Contain("-p:SourceRevisionId=${SOURCE_REVISION}",
            "an argument the publish never reads leaves the assembly stamped with nothing");
        workflow.Should().Contain("SOURCE_REVISION=${{ github.sha }}",
            "and nothing passes it, so every published image would answer unknown");

        SourceText.ReadCode(LoggingPath).Should().Contain("ReleaseInfo.Value",
            "a stamp nobody reads is a string in a file");
    }

    [Fact]
    public void WatchTheStoreFromOutsideItself()
    {
        var scrape = File.ReadAllText(Path.Combine(Root, "docker", "prometheus.yml"));
        var compose = File.ReadAllText(Path.Combine(Root, "docker", "docker-compose.yml"));
        var rules = File.ReadAllText(Path.Combine(Root, "docker", "prometheus", "alerts.yml"));

        // The container name rather than the service name: every target in this
        // file addresses containers, and a service name resolves to nothing here.
        var container = Regex.Match(compose, @"loki:\s*\n\s*container_name:\s*'([\w-]+)'");
        container.Success.Should().BeTrue("the compose file names the log store container");

        var target = Regex.Match(scrape,
            @"job_name:\s*'([\w-]+)'\s*\n\s*static_configs:\s*\n\s*- targets:\s*\['"
            + Regex.Escape(container.Groups[1].Value) + @":\d+'\]");
        target.Success.Should().BeTrue(
            $"nothing scrapes {container.Groups[1].Value}, so the one dependency whose " +
            "failure erases its own evidence is watched by nobody");

        var job = target.Groups[1].Value;
        rules.Should().MatchRegex(@"up\{job=""" + Regex.Escape(job) + @"""\}",
            $"no rule reads up for the {job} job, and a target nobody alerts on is a page nobody opens");
    }

    /// <summary>
    /// The guide filtered severity out of the line body, where it has never been.
    /// </summary>
    /// <remarks>Severity is a label and only a label. Both directions: a selector names a label that exists, and each is in the guide.</remarks>
    [Fact]
    public void QueryTheLabelsThatExistAndOnlyThose()
    {
        var declared = DeclaredLabel.Matches(LoggingSource)
            .Select(match => match.Groups[1].Value)
            .ToList();
        declared.Should().NotBeEmpty("the sink is configured with labels");

        var real = new HashSet<string>(declared) { LevelLabel };

        var used = StreamSelector.Matches(Guide)
            .SelectMany(match => SelectorKey.Matches(match.Groups[1].Value))
            .Select(match => match.Groups[1].Value)
            .ToHashSet();
        used.Should().NotBeEmpty("the guide shows LogQL examples, and finding none passes everything");

        used.Should().BeSubsetOf(real,
            "a selector on a label the sink never attaches returns nothing, which reads " +
            "exactly like a quiet hour");

        foreach (var label in real)
        {
            Guide.Should().Contain($"`{label}`",
                $"the guide states which labels exist, and {label} is one of them");
        }

        Guide.Should().NotMatchRegex(@"\|\s*json\s*\|[^\n]*\blevel\b",
            "severity is a label and never a field of the line, so parsing the body for " +
            "it filters everything away");
    }
}
