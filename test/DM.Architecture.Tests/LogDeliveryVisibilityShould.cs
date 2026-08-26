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
/// Serilog will not let logging throw into the code that logs, so a sink reports
/// nothing about itself by contract. The Loki sink then buffers up to fifty
/// thousand events and retries for ten minutes before dropping them, and all of
/// that is silent: a store answering 400 to every push looks exactly like a quiet
/// application, and Grafana showing nothing looks exactly like a site with nothing
/// to say. The console sink means no content is lost, which is why this is about
/// the searchable copy and not about the logs.
///
/// Three channels close that, and each is asserted here because each is invisible
/// in a green build: the library's own diagnostic stream, a scrape target on the
/// store itself, and a rule reading it. The queries in the guide are asserted with
/// them for the same reason the rules are — a filter naming a field the line does
/// not carry returns an empty page, which is what a healthy hour looks like too.
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
    /// The one label nobody declares. The sink adds it in Grafana's vocabulary
    /// unless told otherwise, so it is real, queryable, and absent from the call
    /// that lists the labels — which is how the guide came to say there were two.
    /// </summary>
    private const string LevelLabel = "level";

    [Fact]
    public void SayOnStderrWhenTheStoreRefusesABatch()
    {
        LoggingSource.Should().Contain("SelfLog.Enable(",
            "the sink swallows its own failures by contract, and this is the only " +
            "channel the library has for saying that a batch was refused");
    }

    /// <summary>
    /// Every line says which build wrote it.
    /// </summary>
    /// <remarks>
    /// Three links, none of which fails visibly on its own: the workflow hands the
    /// commit to the build, the build stamps it into the assembly, and the logger
    /// puts it on the line. Break any one and the process keeps logging, keeps
    /// answering, and starts saying "unknown" where the answer used to be - which
    /// nobody notices until the first incident that needs it.
    /// </remarks>
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

    /// <summary>
    /// The copy that outlives the store carries what the line was about.
    /// </summary>
    /// <remarks>
    /// The console copy is the one the container runtime keeps, and the only one
    /// left when the store is refusing — which is precisely when somebody reads it.
    /// Written with the template a person reads, it holds a timestamp, a level and
    /// the rendered message and drops every property, so the fallback could name
    /// neither the user, nor the correlation token, nor the trace of any line.
    ///
    /// The shape of that copy is asserted where it is chosen; that the two shapes
    /// differ in the right direction is <c>ConsoleCopyShould</c>. What is left here
    /// is the wiring between them, which no compiler checks: the branch can be
    /// dropped and the method it calls left behind, compiling and reverting the fix.
    /// </remarks>
    [Fact]
    public void ShapeTheConsoleCopyByWhoReadsIt()
    {
        LoggingSource.Should().Contain("ConsoleFormat(isDevelopment)",
            "one shape for both readers is what dropped the properties: a server writes " +
            "this copy for a machine and a developer reads it as text");
        LoggingSource.Should().Contain("WriteTo.Console(format)",
            "a chosen formatter that never reaches the sink leaves the default template " +
            "in place, and the choice above it decorative");
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
    /// <remarks>
    /// The body carries Message, MessageTemplate, Exception, TraceId, SpanId and the
    /// event's properties. Severity is a label and only a label, so the documented
    /// query for "errors of the API" selected an empty page — indistinguishable, to
    /// the reader who came to look at errors, from an hour without any.
    ///
    /// Both directions, because the same paragraph is where the count of labels went
    /// stale: a selector may only name a label that exists, and a label that exists
    /// has to be named in the guide.
    /// </remarks>
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
