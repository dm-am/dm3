using System;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// A worker that stopped consuming looks the same as an idle one from its process, so the broker
/// is scraped. Asserted on the configuration files, which live outside the solution.
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

    /// <remarks>
    /// Without the subscriptions to the client's activity sources every consumer trace is a root.
    /// Asserted on the composition: the test host removes the consumer.
    /// </remarks>
    [Fact]
    public void KeepTheTraceOfARequestAcrossTheBroker()
    {
        var composition = TelemetrySources.CompositionCode;

        composition.Should().Contain("RabbitMQ.Client.Publisher",
            "without the publisher source no context is opened around a publish, and the " +
            "consumer has nothing to continue");
        composition.Should().Contain("RabbitMQ.Client.Subscriber",
            "without the subscriber source a consumer opens a root span, and the request " +
            "that sent the message is a separate trace nobody can reach from it");
    }

    private const string MetricsPort = "15692";

    [Fact]
    public void ScrapeTheBrokerItself()
    {
        RepositoryFiles.Read(Plugins).Should().Contain("rabbitmq_prometheus",
            "the metrics endpoint is a plugin, and an image without it enabled answers " +
            "nothing on the metrics port");
        RepositoryFiles.Read(BrokerConfiguration).Should().Contain("return_per_object_metrics",
            "the default endpoint aggregates across every queue, and an aggregate cannot " +
            "name the queue that is backing up");

        var compose = RepositoryFiles.Read(Compose);
        compose.Should().Contain("enabled_plugins",
            "the plugin list has to reach the container");
        compose.Should().Contain(MetricsPort,
            "a port nothing publishes is unreachable from the scraper");

        RepositoryFiles.Read(Scrapes).Should().Contain(MetricsPort,
            "without a target every rule below evaluates over an empty series and stays " +
            "silent for the same reason it would if everything were healthy");
    }

    [Fact]
    public void AlertOnTheQueuesRatherThanOnlyOnTheProcesses()
    {
        var alerts = RepositoryFiles.Read(Alerts);

        alerts.Should().Contain("rabbitmq_queue_messages_ready",
            "a queue growing without bound is the shape of a consumer that stopped, and " +
            "the process it stopped in keeps answering its health probe");
        alerts.Should().Contain("rabbitmq_queue_consumers",
            "a running worker that is subscribed to nothing looks identical to an idle " +
            "one from every metric the worker publishes about itself");
    }

    /// <remarks>
    /// Matched as a prefix, read out of the consumer: the broker appends a suffix per subscription,
    /// so an exact matcher selects no series. Backslashes are dropped before the search.
    /// </remarks>
    [Fact]
    public void AlertOnTheQueueRealtimePushIsDeliveredOver()
    {
        var queue = PushQueueName();
        var alerts = RepositoryFiles.Read(Alerts).Replace("\\", string.Empty, StringComparison.Ordinal);

        alerts.Should().Contain($"{queue}.*",
            "the broker reports this queue under the configured name plus a suffix that is " +
            "new on every subscription, so only a prefix selects it");
        alerts.Should().NotContain($"queue=\"{queue}\"",
            "an equality matcher on the configured name selects no series at any time, " +
            "healthy or broken, which reads as coverage and is silence");
    }

    // Read from the constant: the consumer passes the name by it.
    private static string PushQueueName()
    {
        var declaration = Regex.Match(RepositoryFiles.Read(PushConsumer),
            @"const\s+string\s+QueueName\s*=\s*""([^""]+)""");

        declaration.Success.Should().BeTrue(
            $"{PushConsumer} declares the queue it subscribes to, and a walk that cannot " +
            "find it checks nothing");
        return declaration.Groups[1].Value;
    }

    [Fact]
    public void MeasureMessagesOnTheConsumerDashboard()
    {
        var dashboard = RepositoryFiles.Read(Dashboard);

        dashboard.Should().Contain("rabbitmq_queue_",
            "the board is named after the consumers and has to answer whether they are " +
            "consuming");
        dashboard.Should().Contain("dm_messaging_consumed_total",
            "a short queue with a consumer on it is what a worker throwing on every message " +
            "looks like too, so the queue side alone cannot tell work from the appearance " +
            "of work");
        dashboard.Should().NotContain("http_server_request_duration_seconds_count",
            "the workers serve nothing but their own health probe, so their request rate " +
            "is the scrape interval drawn as a line");
    }
}
