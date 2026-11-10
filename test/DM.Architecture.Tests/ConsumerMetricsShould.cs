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

// Only the consumer knows whether the work it took succeeded; series coverage is
// MetricSeriesShould's.
public class ConsumerMetricsShould
{
    [Fact]
    public void MeasureEveryMessageThatPassesThroughAConsumer()
    {
        var middlewares = Directory
            .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src"), "*Middleware.cs", SearchOption.AllDirectories)
            .Where(RepositoryFiles.IsAuthored)
            .Where(path => File.ReadAllText(path).Contains(ConsumerSide, StringComparison.Ordinal))
            .ToArray();

        middlewares.Should().NotBeEmpty(
            "the hosts consume through middlewares of this shape, and a walk that finds none " +
            "of them checks nothing");

        middlewares
            .Where(path => !Measures(File.ReadAllText(path)))
            .Select(RepositoryFiles.Relative)
            .Should().BeEmpty(
                "every message of a consumer passes through its middleware, so this is " +
                "the one place that can count them; without it a consumer failing everything " +
                "is indistinguishable from an idle one");

        var hosts = ConsumingHosts();
        hosts.Should().NotBeEmpty(
            "three hosts take messages off a queue, and a walk that finds none of them to " +
            "read passes whatever they do");

        foreach (var host in hosts)
        {
            var installed = InstalledConsumerMiddlewares(
                File.ReadAllText(Path.Combine(host.FullName, Composition)));

            installed.Should().NotBeEmpty(
                $"{host.Name} takes messages off a queue, and a host whose consumer pipeline " +
                "installs nothing counts nothing of what it took");

            installed
                .Where(name => !middlewares.Any(path => path.EndsWith(
                    $"{Path.DirectorySeparatorChar}{name}.cs", StringComparison.Ordinal)))
                .Should().BeEmpty(
                    $"{host.Name} consumes through a middleware no file of this walk measures, " +
                    "and a queue read through one is a queue whose failures reach no dashboard " +
                    "and no rule");
        }
    }

    private const string Composition = "Startup.cs";

    private const string ConsumerFactory = "IDmConsumerBuilder";

    /// <summary>A middleware installed into a pipeline, as the declaration spells it.</summary>
    private static readonly Regex Installation = new(@"AddDmConsumerMiddleware<(\w+)>", RegexOptions.Compiled);

    /// <summary>
    /// The hosts that consume, read off the tree rather than listed here.
    /// </summary>
    private static IReadOnlyCollection<DirectoryInfo> ConsumingHosts() =>
        new DirectoryInfo(Path.Combine(RepositoryLayout.Root, "src"))
            .EnumerateDirectories()
            .Where(project => File.Exists(Path.Combine(project.FullName, Composition)))
            .Where(Consumes)
            .ToArray();

    private static bool Consumes(DirectoryInfo project) => project
        .EnumerateFiles("*.cs", SearchOption.AllDirectories)
        .Where(file => RepositoryFiles.IsAuthored(file.FullName))
        .Any(file => File.ReadAllText(file.FullName).Contains(ConsumerFactory, StringComparison.Ordinal));

    // The declaring call is generic over the middleware type the message scope resolves.
    private static IReadOnlyCollection<string> InstalledConsumerMiddlewares(string composition) =>
        Installation.Matches(composition).Select(match => match.Groups[1].Value).ToArray();

    // Not by file name: the realtime push of the API has no retry to name a file after.
    private const string ConsumerSide = "IConsumerMiddleware";

    private const string SharedPipeline = "MeasuredConsumerPipeline";

    // The middlewares share one copy of the counters through the helper.
    private static bool Measures(string source) =>
        source.Contains("MessagingMetrics", StringComparison.Ordinal) ||
        source.Contains(SharedPipeline, StringComparison.Ordinal);

    private static readonly Regex MeterOwner = new(
        @"class\s+(\w+)[\s\S]{0,400}?const\s+string\s+MeterName", RegexOptions.Compiled);

    // An unregistered meter still records in process memory and exports no series.
    [Fact]
    public void RegisterEveryMeterWithTheExporter()
    {
        var registration = TelemetrySources.CompositionCode;

        var owners = MetricsSources()
            .SelectMany(path => MeterOwner.Matches(File.ReadAllText(path)))
            .Select(match => match.Groups[1].Value)
            .ToList();

        owners.Should().NotBeEmpty(
            "the sources declare meters, and a walk that finds none of them passes everything");

        foreach (var owner in owners)
        {
            registration.Should().Contain($"{owner}.MeterName",
                $"{owner} declares a meter the provider is never told about, so its " +
                "instruments record into process memory and are exported nowhere - which " +
                "looks from the inside exactly like instrumentation that works");
        }
    }

    // Named rather than derived: whether a failure has a caller left is not in the declaration.
    private static readonly (string Owner, string Why)[] Unattended =
    [
        // Nobody waits on a delivery, and a refused channel is swallowed so the others do not repeat.
        ("MessagingMetrics", "a delivery that fails answers nobody"),
        // The document write runs after the relational commit, with nothing left to unwind.
        ("StorageMetrics", "a dropped write is answered as a success"),
    ];

    // A refused outbox publish is a delay, alerted through the lag gauge.
    private static readonly Dictionary<string, string> AlertedThroughAnotherSeries =
        new(StringComparer.Ordinal)
        {
            ["dm_messaging_outbox_publish_failed"] = "dm_messaging_outbox_lag",
        };

    // There is no caller to return the error to, so a counter no rule reads is silence.
    [Fact]
    public void AlertOnEveryFailureNobodyIsWaitingOn()
    {
        var rules = File.ReadAllText(
            Path.Combine(RepositoryLayout.Root, "docker", "prometheus", "alerts.yml"));

        foreach (var (owner, why) in Unattended)
        {
            var failures = InstrumentsOf(owner)
                .Where(name => name.Contains("_failed", StringComparison.Ordinal)
                    || name.Contains("_lost", StringComparison.Ordinal))
                .ToList();

            failures.Should().NotBeEmpty(
                $"{owner} is listed as measuring work nobody is waiting on, and a meter with " +
                "no failure among its instruments does not belong on that list");

            foreach (var failure in failures)
            {
                var alerted = AlertedThroughAnotherSeries.GetValueOrDefault(failure, failure);
                rules.Should().Contain(alerted,
                    $"{failure} counts work that was lost while {why}, and a count no rule " +
                    "reads is the same silence as no count at all");
            }
        }

        foreach (var (excused, _) in AlertedThroughAnotherSeries)
        {
            Unattended.SelectMany(entry => InstrumentsOf(entry.Owner)).Should().Contain(excused,
                $"the exemption for {excused} names an instrument that is gone");
        }
    }

    /// <remarks>
    /// Each sender catches its own failures, so the pipeline never counts them. By the file, so a
    /// new channel is asked the same.
    /// </remarks>
    [Fact]
    public void CountWhatEverySenderSwallows()
    {
        var senders = Directory
            .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src", "DM.Workers.NotificationDispatcher"),
                "*Sender.cs", SearchOption.AllDirectories)
            .Where(RepositoryFiles.IsAuthored)
            .Where(path => !Path.GetFileName(path).StartsWith("I", StringComparison.Ordinal))
            .ToList();

        senders.Should().NotBeEmpty(
            "the dispatcher delivers through senders, and a walk that finds none of them " +
            "passes everything");

        senders
            .Where(path => !File.ReadAllText(path).Contains(
                "MessagingMetrics.DeliveryFailed", StringComparison.Ordinal))
            .Select(RepositoryFiles.Relative)
            .Should().BeEmpty(
                "a sender swallows what its channel refused so that the other channels are " +
                "not replayed, and a swallowed failure nobody counts is a reader who never " +
                "got the notification and nothing anywhere saying so");
    }

    // Past the limiter the reason is recorded; before it only the status on the request series.
    [Fact]
    public void AlertOnRefusalsAtTheFrontDoor()
    {
        var rules = File.ReadAllText(
            Path.Combine(RepositoryLayout.Root, "docker", "prometheus", "alerts.yml"));

        foreach (var refusal in InstrumentsOf("AuthenticationMetrics"))
        {
            rules.Should().Contain(refusal,
                $"{refusal} carries the reason a login was refused, which is the one thing " +
                "the request series cannot say");
        }

        rules.Should().MatchRegex(@"http_response_status_code=""429""",
            "the limiter answers before any code that could count a reason runs, so a run " +
            "being turned away at the door is visible only as a status");
    }
}
