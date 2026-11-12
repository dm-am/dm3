using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// A worker that stopped draining its queue looks the same as an idle one from its process, so the
/// queue is watched from the table. Asserted on the configuration files, which live outside the solution.
/// </remarks>
public class QueueObservabilityShould
{
    private const string Alerts = "docker/prometheus/alerts.yml";
    private const string Dashboard = "docker/grafana/dashboards/dm-consumers.json";

    [Fact]
    public void AlertOnTheQueuesRatherThanOnlyOnTheProcesses()
    {
        var alerts = RepositoryFiles.Read(Alerts);

        alerts.Should().Contain("dm_messaging_outbox_dead",
            "a row no worker will take again waits in the table, and the process that gave " +
            "up on it keeps answering its health probe");
        alerts.Should().Contain("dm_messaging_outbox_lag_seconds",
            "a running worker that claims nothing looks identical to an idle one from every " +
            "metric the worker publishes about itself, and only the age of the oldest ready " +
            "row tells them apart");
        alerts.Should().ContainAll(
            ["max by (queue) (dm_messaging_outbox_dead)", "max by (queue) (dm_messaging_outbox_lag_seconds)"],
            "every API instance reads the same table, so a rule on the raw gauge raises one alert " +
            "per instance and resolves when the instance it was keyed to restarts");
    }

    [Fact]
    public void MeasureTheQueuesOnTheConsumerDashboard()
    {
        var dashboard = RepositoryFiles.Read(Dashboard);

        dashboard.Should().ContainAll(
            ["dm_messaging_outbox_pending", "dm_messaging_outbox_lag_seconds", "dm_messaging_outbox_dead"],
            "the board is named after the workers and has to answer whether they are draining " +
            "their queues");
        dashboard.Should().Contain("dm_messaging_consumed_total",
            "a short queue is what a worker failing on every row looks like too, so the queue " +
            "side alone cannot tell work from the appearance of work");
        dashboard.Should().NotContain("http_server_request_duration_seconds_count",
            "the workers serve nothing but their own health probe, so their request rate " +
            "is the scrape interval drawn as a line");
    }
}
