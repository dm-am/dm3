using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;

namespace DM.Architecture.Tests;

/// <remarks>
/// Two rule classes read the same declarations from opposite ends:
/// ConsumerMetricsShould asks whether what a meter counts is registered and
/// read by a rule, MetricSeriesShould asks whether what a rule or a board names
/// is declared. The spelling of a declaration is the one thing both depend on,
/// so it lives here once, beside TelemetrySources, which reads the place the
/// providers are built.
/// </remarks>
internal static class MeterSources
{
    internal static readonly Regex Instrument = new(
        @"Create(?:Counter|Histogram|UpDownCounter|ObservableGauge|ObservableCounter|Gauge)<[^>]+>\(\s*""(dm\.[a-z0-9._]+)""",
        RegexOptions.Compiled);

    internal static IReadOnlyCollection<string> InstrumentsOf(string owner)
    {
        var source = MetricsSources()
            .FirstOrDefault(path => Path.GetFileNameWithoutExtension(path) == owner);

        source.Should().NotBeNull($"{owner} is the class this rule is about");

        return Instrument.Matches(File.ReadAllText(source!))
            .Select(match => match.Groups[1].Value.Replace('.', '_'))
            .ToArray();
    }

    internal static IEnumerable<string> MetricsSources() => Directory
        .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src"), "*Metrics.cs", SearchOption.AllDirectories)
        .Where(RepositoryFiles.IsAuthored);
}
