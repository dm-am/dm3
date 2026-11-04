using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;
using static DM.Architecture.Tests.MeterSources;

namespace DM.Architecture.Tests;

/// <summary>
/// Every rule and every panel written about a series names a series something
/// actually publishes.
/// </summary>
/// <remarks>
/// A rule over a series nobody exports is as silent as a healthy one. Names are derived as the
/// exporter derives them: dots to underscores, _total on a counter, a non-UCUM unit appended verbatim.
/// </remarks>
public class MetricSeriesShould
{
    /// <summary>A series of this project, as an expression spells it.</summary>
    private static readonly Regex Series = new(@"\bdm_[a-z0-9_]+", RegexOptions.Compiled);

    /// <summary>Any series name, whoever exports it.</summary>
    /// <remarks>Wider than the one above: a rule over an exporter's series is as silent over a typo.</remarks>
    private static readonly Regex AnySeries = new(
        @"(?<![\w.""])[a-z][a-z0-9]*_[a-z0-9_]+", RegexOptions.Compiled);

    /// <summary>
    /// The prefix of a series nobody here declares, and the scrape job that brings it in; both halves asserted.
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
        // The alerting contour is scraped like any other target.
        ("alertmanager_", "alertmanager"),
        ("prometheus_", "prometheus"),
    ];

    /// <summary>
    /// Words that look like a series and are not: PromQL functions, label names and the plugin name in prose.
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
    /// Without a running stack only the pairing is asserted: the name belongs to an exporter and that
    /// exporter is scraped. A misspelt series inside a scraped prefix still passes.
    /// </remarks>
    [Fact]
    public void DrawOnlyOnExportersPrometheusScrapes()
    {
        var scrape = File.ReadAllText(
            Path.Combine(RepositoryLayout.Root, "docker", "prometheus.yml"));
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

    /// <summary>An instrument declaration with its unit argument.</summary>
    private static readonly Regex InstrumentWithUnit = new(
        @"Create(?<kind>Counter|Histogram|UpDownCounter|ObservableGauge|ObservableCounter|Gauge)<[^>]+>\(\s*""(?<name>dm\.[a-z0-9._]+)""\s*,\s*(?<unit>null|""[^""]*"")",
        RegexOptions.Compiled);

    /// <summary>
    /// Units the Prometheus exporter knows how to turn into a suffix.
    /// </summary>
    /// <remarks>The exporter maps the UCUM table and appends anything else verbatim.</remarks>
    private static readonly HashSet<string> Units = new(StringComparer.Ordinal) { "s", "By" };

    /// <summary>
    /// A measurement carries its unit and a count carries none.
    /// </summary>
    /// <remarks>A wrong unit gives a name nobody would write from the declaration.</remarks>
    [Fact]
    public void DeclareOnlyUnitsTheExporterUnderstands()
    {
        var declarations = RepositoryFiles.ProductionSources()
            .SelectMany(path => InstrumentWithUnit.Matches(File.ReadAllText(path)))
            .Select(match => (
                Name: match.Groups["name"].Value,
                Kind: match.Groups["kind"].Value,
                Unit: match.Groups["unit"].Value.Trim('"')))
            .ToList();

        declarations.Should().NotBeEmpty(
            "the expression has to find the declarations, and one that matches none passes " +
            "whatever they say");

        foreach (var (name, kind, unit) in declarations)
        {
            if (kind.EndsWith("Counter", StringComparison.Ordinal))
            {
                unit.Should().Be("null",
                    $"{name} counts things, and a word the exporter does not know is appended " +
                    "verbatim in front of _total");
                continue;
            }

            if (kind == "Gauge" && unit == "null")
            {
                // A level gauge over a count is dimensionless: UCUM has nothing to map "rows" to.
                continue;
            }

            unit.Should().NotBe("null", $"{name} measures something, and a measurement has a unit");
            Units.Should().Contain(unit,
                $"{name} is declared in a unit the exporter cannot map, so it exports under a " +
                "name nobody would write a rule from");
        }
    }

    /// <summary>
    /// Every histogram of this project has boundaries somebody chose.
    /// </summary>
    /// <remarks>The SDK's default ladder tops out at ten seconds, picked for nothing measured here.</remarks>
    [Fact]
    public void ChooseTheBucketsOfEveryHistogram()
    {
        var declared = RepositoryFiles.ProductionSources()
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"CreateHistogram<[^>]+>\(\s*""(dm\.[a-z0-9._]+)"""))
            .Select(match => match.Groups[1].Value)
            .ToList();

        declared.Should().NotBeEmpty("the expression has to find the histograms it is about");

        var configured = Regex
            .Matches(TelemetrySources.CompositionCode, @"AddView\(\s*""(dm\.[a-z0-9._]+)""")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        declared.Should().BeSubsetOf(configured,
            "a histogram nobody configured is one measured against the defaults of the SDK");
        configured.Should().BeSubsetOf(declared,
            "a view naming an instrument that no longer exists configures nothing and reads " +
            "like it configures something");
    }

    /// <summary>Instrument names declared in the sources, as the exporter sanitises them.</summary>
    private static HashSet<string> Published()
    {
        var fromCode = RepositoryFiles.ProductionSources()
            .SelectMany(path => Instrument.Matches(File.ReadAllText(path)))
            .Select(match => match.Groups[1].Value.Replace('.', '_'));

        return fromCode.Concat(FromTextfileCollector()).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Series a maintenance script writes for node-exporter to pick up.
    /// </summary>
    /// <remarks>
    /// The nightly backup check leaves its verdict in the textfile collector's directory. Read from the
    /// scripts and keyed on the stem, as a C# instrument is declared without its unit suffix.
    /// </remarks>
    private static IEnumerable<string> FromTextfileCollector() => Directory
        .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "docker", "scripts"), "*.sh")
        .SelectMany(path => TextfileSeries.Matches(File.ReadAllText(path)))
        .Select(match => Base(match.Groups[1].Value));

    /// <summary>A HELP line, which is what names a series in the text exposition format.</summary>
    private static readonly Regex TextfileSeries = new(
        @"#\s*HELP\s+(dm_\w+)", RegexOptions.Compiled);

    /// <summary>Series named by the rule file and the boards.</summary>
    private static IReadOnlyCollection<(string File, string Series)> Referenced() =>
        new[] { Path.Combine("docker", "prometheus", "alerts.yml") }
            .Concat(Directory
                .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "docker", "grafana", "dashboards"), "*.json")
                .Select(RepositoryFiles.Relative))
            .SelectMany(relative => Series
                .Matches(File.ReadAllText(Path.Combine(RepositoryLayout.Root, relative)))
                .Select(match => (File: relative, Series: match.Value)))
            .Distinct()
            .ToArray();

    /// <summary>Every series-shaped name in the rules and the boards.</summary>
    private static IReadOnlyCollection<(string File, string Series)> ReferencedAnywhere() =>
        MonitoringFiles()
            .SelectMany(relative => AnySeries
                .Matches(File.ReadAllText(Path.Combine(RepositoryLayout.Root, relative)))
                .Select(match => (File: relative, Series: match.Value)))
            .Where(reference => !NotASeries.Contains(reference.Series))
            .Distinct()
            .ToArray();

    private static IEnumerable<string> MonitoringFiles() =>
        new[] { Path.Combine("docker", "prometheus", "alerts.yml") }
            .Concat(Directory
                .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "docker", "grafana", "dashboards"), "*.json")
                .Select(RepositoryFiles.Relative));

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
}
