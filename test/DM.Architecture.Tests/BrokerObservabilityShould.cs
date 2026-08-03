using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The state of the bus is measured, not inferred from the processes on it.
/// </summary>
/// <remarks>
/// The consumers dashboard showed the request rate of the workers - traffic of
/// their own health probes - their GC heap, and whether their process answered.
/// All of that is equally true of a worker that has silently stopped consuming,
/// and none of it says anything about a queue. Nothing scraped the broker at
/// all, so a message stuck in a queue, a dead-letter queue filling up and a
/// queue left with no consumer were three states indistinguishable from an idle
/// Saturday, and the way any of them got found was a reader saying a
/// notification never came.
///
/// Asserted on the configuration files, because the whole of this lives outside
/// the solution: a plugin, a scrape target, a rule file and a dashboard.
/// </remarks>
public class BrokerObservabilityShould
{
    private const string Compose = "docker/docker-compose.yml";
    private const string Plugins = "docker/rabbitmq/enabled_plugins";
    private const string BrokerConfiguration = "docker/rabbitmq/dm.conf";
    private const string Scrapes = "docker/prometheus.yml";
    private const string Alerts = "docker/prometheus/alerts.yml";
    private const string Dashboard = "docker/grafana/dashboards/dm-consumers.json";

    /// <summary>Port the metrics plugin answers on.</summary>
    private const string MetricsPort = "15692";

    [Fact]
    public void ScrapeTheBrokerItself()
    {
        Read(Plugins).Should().Contain("rabbitmq_prometheus",
            "the metrics endpoint is a plugin, and an image without it enabled answers " +
            "nothing on the metrics port");
        Read(BrokerConfiguration).Should().Contain("return_per_object_metrics",
            "the default endpoint aggregates across every queue, and an aggregate cannot " +
            "name the queue that is backing up");

        var compose = Read(Compose);
        compose.Should().Contain("enabled_plugins",
            "the plugin list has to reach the container");
        compose.Should().Contain(MetricsPort,
            "a port nothing publishes is unreachable from the scraper");

        Read(Scrapes).Should().Contain(MetricsPort,
            "without a target every rule below evaluates over an empty series and stays " +
            "silent for the same reason it would if everything were healthy");
    }

    [Fact]
    public void AlertOnTheQueuesRatherThanOnlyOnTheProcesses()
    {
        var alerts = Read(Alerts);

        alerts.Should().Contain("rabbitmq_queue_messages_ready",
            "a queue growing without bound is the shape of a consumer that stopped, and " +
            "the process it stopped in keeps answering its health probe");
        alerts.Should().Contain("rabbitmq_queue_consumers",
            "a running worker that is subscribed to nothing looks identical to an idle " +
            "one from every metric the worker publishes about itself");
    }

    [Fact]
    public void MeasureMessagesOnTheConsumerDashboard()
    {
        var dashboard = Read(Dashboard);

        dashboard.Should().Contain("rabbitmq_queue_",
            "the board is named after the consumers and has to answer whether they are " +
            "consuming");
        dashboard.Should().NotContain("http_server_request_duration_seconds_count",
            "the workers serve nothing but their own health probe, so their request rate " +
            "is the scrape interval drawn as a line");
    }

    private static string Read(string relative)
    {
        var path = Path.Combine(
            RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(path).Should().BeTrue($"{relative} must exist at {path}");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Walks up from the test binary to the repository root. The configuration is
    /// not copied to the output directory, and copying it would let this assert
    /// against a stale snapshot.
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
