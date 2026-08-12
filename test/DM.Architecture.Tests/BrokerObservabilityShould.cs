using System;
using System.IO;
using System.Text.RegularExpressions;
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
    private const string PushConsumer = "src/DM.Web.API/Realtime/RealtimeNotificationConsumer.cs";

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

    /// <summary>
    /// The queue realtime push is delivered over is a queue too.
    /// </summary>
    /// <remarks>
    /// The rule about queues without a consumer named the two work queues and
    /// stopped there, so the one the API subscribes to sat outside every rule in
    /// the file: both workers answered, both named queues had their consumers, and
    /// pushes were dropped at the broker with nothing anywhere saying so.
    ///
    /// The name is read out of the consumer instead of being repeated here, and it
    /// is required as a prefix, because the broker does not report this queue under
    /// the name the code configures - it appends a suffix that is new on every
    /// subscription. Which is why an exact matcher is not coverage but its
    /// opposite: it selects no series at any time, so `== 0` never fires and
    /// absent() always holds, and a rule built on it is either mute or permanently
    /// firing.
    ///
    /// Backslashes are dropped before the search, so escaping the dots of the
    /// regex - which changes nothing about what it matches here - does not turn
    /// this red.
    /// </remarks>
    [Fact]
    public void AlertOnTheQueueRealtimePushIsDeliveredOver()
    {
        var queue = PushQueueName();
        var alerts = Read(Alerts).Replace("\\", string.Empty, StringComparison.Ordinal);

        alerts.Should().Contain($"{queue}.*",
            "the broker reports this queue under the configured name plus a suffix that is " +
            "new on every subscription, so only a prefix selects it");
        alerts.Should().NotContain($"queue=\"{queue}\"",
            "an equality matcher on the configured name selects no series at any time, " +
            "healthy or broken, which reads as coverage and is silence");
    }

    /// <summary>The queue the API subscribes to, as its consumer declares it.</summary>
    /// <remarks>
    /// Read from the constant rather than from the argument of the parameters:
    /// the consumer passes the name by that constant, so a rule matching a
    /// literal in the call finds nothing and fails on its own reading rather
    /// than on the thing it is about.
    /// </remarks>
    private static string PushQueueName()
    {
        var declaration = Regex.Match(Read(PushConsumer),
            @"const\s+string\s+QueueName\s*=\s*""([^""]+)""");

        declaration.Success.Should().BeTrue(
            $"{PushConsumer} declares the queue it subscribes to, and a walk that cannot " +
            "find it checks nothing");
        return declaration.Groups[1].Value;
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

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
