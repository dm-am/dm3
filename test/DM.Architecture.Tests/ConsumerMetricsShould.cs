using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A consumer says what happened to the messages it took, and every rule written
/// about that names a series something actually publishes.
/// </summary>
/// <remarks>
/// The broker knows how much work is waiting; only the consumer knows whether
/// the work it took succeeded. A worker that threw on every message kept its
/// queue short, its subscription alive and its process answering, so every rule
/// about the queue and every panel about the process stayed green, and the one
/// trace of the failures was a warning line nothing watched.
///
/// The second half is the other way the hole reappears: a rule whose expression
/// names a series nobody exports evaluates over nothing and is silent for the
/// same reason it would be if everything were healthy. Names are compared
/// against the instruments declared in the sources, because the exporter derives
/// the exported name from those - dots become underscores, a counter gains
/// _total, and a unit outside the UCUM table is appended verbatim.
/// </remarks>
public class ConsumerMetricsShould
{
    /// <summary>Instrument declaration, as every metrics class spells it.</summary>
    private static readonly Regex Instrument = new(
        @"Create(?:Counter|Histogram|UpDownCounter|ObservableGauge|ObservableCounter)<[^>]+>\(\s*""(dm\.[a-z0-9._]+)""",
        RegexOptions.Compiled);

    /// <summary>A series of this project, as an expression spells it.</summary>
    private static readonly Regex Series = new(@"\bdm_[a-z0-9_]+", RegexOptions.Compiled);

    /// <summary>Any series name, whoever exports it.</summary>
    /// <remarks>
    /// Deliberately wider than the one above. Restricting the walk to dm_ left the
    /// rules and the boards free to name anything else — including the very series
    /// the previous wave put in place of two panels that had been drawing on
    /// metrics nobody exported. A rule over rabbitmq_queue_messages_ready is as
    /// silent as a rule over a typo, and the walk that was supposed to catch that
    /// class of defect could not see it.
    /// </remarks>
    private static readonly Regex AnySeries = new(
        @"(?<![\w.""])[a-z][a-z0-9]*_[a-z0-9_]+", RegexOptions.Compiled);

    /// <summary>
    /// The prefix of a series nobody here declares, and the scrape job that brings
    /// it in. Both halves are asserted, so a prefix outliving its job is caught and
    /// so is a job nobody draws on.
    /// </summary>
    private static readonly (string Prefix, string Job)[] Exporters =
    [
        // The .NET hosts: ASP.NET Core and the runtime instrument themselves, and the
        // three jobs below scrape /metrics off the same port the app serves on.
        ("http_server_", "dm-api"),
        ("http_client_", "dm-api"),
        ("process_runtime_", "dm-api"),
        ("rabbitmq_", "rabbitmq"),
        ("pg_", "postgres"),
        ("node_", "node"),
    ];

    /// <summary>
    /// Words that look like a series and are not: PromQL functions, label names,
    /// and the plugin name in prose. Listed rather than pattern-matched, because
    /// what separates them from a series is what they mean and not how they read.
    /// </summary>
    private static readonly HashSet<string> NotASeries = new(StringComparer.Ordinal)
    {
        "histogram_quantile", "label_replace", "label_join", "clamp_max", "clamp_min",
        "http_route", "http_response_status_code", "job_name", "scrape_interval",
        "static_configs", "rabbitmq_prometheus", "rule_files", "metrics_path",
    };

    /// <summary>What the Prometheus exporter appends to a name of its own accord.</summary>
    private static readonly string[] Suffixes =
        ["_total", "_bucket", "_sum", "_count", "_seconds", "_bytes", "_ratio"];

    [Fact]
    public void MeasureEveryMessageThatPassesThroughAConsumer()
    {
        var middlewares = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*RetryMiddleware.cs", SearchOption.AllDirectories)
            .Where(IsAuthored)
            .ToArray();

        middlewares.Should().HaveCountGreaterOrEqualTo(2,
            "both workers wrap their pipeline in one, and a walk that finds fewer checks nothing");

        middlewares
            .Where(path => !File.ReadAllText(path).Contains("MessagingMetrics", StringComparison.Ordinal))
            .Select(Relative)
            .Should().BeEmpty(
                "every message of a worker passes through its retry middleware, so this is " +
                "the one place that can count them; without it a consumer failing everything " +
                "is indistinguishable from an idle one");
    }

    [Fact]
    public void RegisterTheMeterWithTheExporter() =>
        File.ReadAllText(Path.Combine(
                RepositoryRoot, "src", "DM.Infrastructure.Core", "Logging", "LoggingConfiguration.cs"))
            .Should().Contain("MessagingMetrics.MeterName",
                "an instrument nobody added to the meter provider is written at runtime and " +
                "exported nowhere, which looks exactly like no instrumentation at all");

    [Fact]
    public void AlertAndDrawOnlyOnSeriesSomethingPublishes()
    {
        var published = Published();
        published.Should().NotBeEmpty("the sources declare instruments, and finding none passes everything");

        var referenced = Referenced();
        referenced.Should().NotBeEmpty(
            "the rules and the boards name series of this project, and finding none passes everything");

        referenced
            .Where(reference => !published.Contains(Base(reference.Series)))
            .Select(reference => $"{reference.File}: {reference.Series}")
            .Should().BeEmpty(
                "an expression over a series nobody exports returns nothing, and a rule that " +
                "returns nothing is quiet in exactly the way a healthy system is");
    }

    /// <summary>
    /// A series this project does not export comes from a job it scrapes.
    /// </summary>
    /// <remarks>
    /// The other half of the same defect. Two panels of the consumer board used to
    /// draw on metrics no consumer publishes, and they were replaced by
    /// rabbitmq_queue_* — which is right, and which the check above could not see,
    /// because it only ever looked at names beginning with dm_. Anything from an
    /// exporter was outside the walk entirely: the queue series, the runtime
    /// series, the host series. What can be asserted without a running stack is the
    /// pairing — the name belongs to an exporter, and that exporter is scraped.
    ///
    /// The limit of it, stated so nobody reads more into a green run than is there:
    /// within a prefix that is scraped, a misspelt series still passes. Telling
    /// rabbitmq_queue_consumers from rabbitmq_queue_consumerz needs a /metrics
    /// response, which needs the stack up, which is the CI step this replaces and
    /// does not equal. What it does catch is a whole exporter nobody scrapes and a
    /// name belonging to no exporter at all — the two ways a board or a rule ends up
    /// evaluating over nothing for the life of the file.
    /// </remarks>
    [Fact]
    public void DrawOnlyOnExportersPrometheusScrapes()
    {
        var scrape = File.ReadAllText(
            Path.Combine(RepositoryRoot, "docker", "prometheus.yml"));
        var jobs = Regex.Matches(scrape, @"job_name:\s*'([\w-]+)'")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        jobs.Should().NotBeEmpty("the scrape configuration names the jobs");

        var published = Published();
        var foreign = ReferencedAnywhere()
            .Where(reference => !reference.Series.StartsWith("dm_", StringComparison.Ordinal))
            .ToList();

        foreign.Should().NotBeEmpty(
            "the rules and the boards read the exporters too, and finding none passes everything");

        foreach (var (file, series) in foreign)
        {
            var exporter = Exporters.FirstOrDefault(e =>
                series.StartsWith(e.Prefix, StringComparison.Ordinal));

            exporter.Prefix.Should().NotBeNull(
                $"{file} names {series}, which no exporter in this stack is known to publish; " +
                "an expression over a name nobody exports evaluates over nothing and stays " +
                "silent for the same reason it would if everything were healthy");
            jobs.Should().Contain(exporter.Job,
                $"{file} draws on {series} from the {exporter.Job} exporter, and nothing " +
                "scrapes it");
        }

        // Both directions, so an entry cannot outlive what it excused.
        foreach (var (prefix, job) in Exporters)
        {
            jobs.Should().Contain(job,
                $"the {prefix} prefix is excused by the {job} job, which no longer exists");
        }

        published.Should().NotBeEmpty("this project exports series of its own too");
    }

    /// <summary>Instrument names declared in the sources, as the exporter sanitises them.</summary>
    private static HashSet<string> Published() => Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
        .Where(IsAuthored)
        .SelectMany(path => Instrument.Matches(File.ReadAllText(path)))
        .Select(match => match.Groups[1].Value.Replace('.', '_'))
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>Series named by the rule file and the boards.</summary>
    private static IReadOnlyCollection<(string File, string Series)> Referenced() =>
        new[] { Path.Combine("docker", "prometheus", "alerts.yml") }
            .Concat(Directory
                .EnumerateFiles(Path.Combine(RepositoryRoot, "docker", "grafana", "dashboards"), "*.json")
                .Select(Relative))
            .SelectMany(relative => Series
                .Matches(File.ReadAllText(Path.Combine(RepositoryRoot, relative)))
                .Select(match => (File: relative, Series: match.Value)))
            .Distinct()
            .ToArray();

    /// <summary>Every series-shaped name in the rules and the boards.</summary>
    private static IReadOnlyCollection<(string File, string Series)> ReferencedAnywhere() =>
        MonitoringFiles()
            .SelectMany(relative => AnySeries
                .Matches(File.ReadAllText(Path.Combine(RepositoryRoot, relative)))
                .Select(match => (File: relative, Series: match.Value)))
            .Where(reference => !NotASeries.Contains(reference.Series))
            .Distinct()
            .ToArray();

    private static IEnumerable<string> MonitoringFiles() =>
        new[] { Path.Combine("docker", "prometheus", "alerts.yml") }
            .Concat(Directory
                .EnumerateFiles(Path.Combine(RepositoryRoot, "docker", "grafana", "dashboards"), "*.json")
                .Select(Relative));

    /// <summary>Strips whatever the exporter appended, down to the instrument name.</summary>
    private static string Base(string series)
    {
        var trimmed = series;
        bool stripped;
        do
        {
            stripped = false;
            foreach (var suffix in Suffixes)
            {
                if (trimmed.EndsWith(suffix, StringComparison.Ordinal))
                {
                    trimmed = trimmed[..^suffix.Length];
                    stripped = true;
                }
            }
        }
        while (stripped);

        return trimmed;
    }

    private static bool IsAuthored(string path) => !path
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(segment => segment is "obj" or "bin" or "node_modules");

    private static string Relative(string path) => Path.GetRelativePath(RepositoryRoot, path);

    /// <summary>
    /// Walks up from the test binary to the repository root. Neither the sources
    /// nor the monitoring configuration is copied to the output directory, and
    /// copying them would let this assert against a stale snapshot.
    /// </summary>
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null &&
                   !(Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                     Directory.Exists(Path.Combine(directory.FullName, "test"))))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!.FullName;
        }
    }
}
