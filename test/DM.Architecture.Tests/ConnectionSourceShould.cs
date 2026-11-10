using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Domain.Core.Mail;
using DM.Infrastructure.Core.Queues;
using DM.Infrastructure.Messaging.Queues;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// Observed over the composed services by closing the process's source: whatever still
/// connects afterwards took its connections from somewhere else. Each rule composes a host
/// of its own, because the closed source would break every rule over the shared ones.
/// </remarks>
public class ConnectionSourceShould
{
    [Fact]
    public async Task FeedTheDataLayerAndTheDatabaseCheckOfTheApi()
    {
        await using var provider = Compose("DM.Web.API");

        await provider.GetRequiredService<NpgsqlDataSource>().DisposeAsync();

        DataLayerOpening(provider).Should().Throw<ObjectDisposedException>(
            "the data layer has to take its connections from the process's one source, " +
            "or the queue and the metrics of later tickets open a second pool beside it");

        var report = await provider.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(check => check.Name == "postgresql");
        report.Entries["postgresql"].Exception.Should().BeOfType<ObjectDisposedException>(
            "the database check has to probe the same source the data layer draws from, " +
            "or it reports on a pool nobody uses");
    }

    /// <remarks>Mutation it must fail on: a check of a service the API no longer depends on.</remarks>
    [Fact]
    public void CheckNothingButTheDatabaseAndTheListenerInTheApi()
    {
        using var provider = Compose("DM.Web.API");

        provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations.Select(check => check.Name)
            .Should().BeEquivalentTo(["postgresql", "realtime-push"],
                "pushes reach the instance through the database, so it depends on the database " +
                "and on its own listener alone");
    }

    [Fact]
    public async Task FeedTheDataLayerOfTheNotificationWorker()
    {
        await using var provider = Compose("DM.Workers.NotificationDispatcher");

        await provider.GetRequiredService<NpgsqlDataSource>().DisposeAsync();

        DataLayerOpening(provider).Should().Throw<ObjectDisposedException>(
            "the worker's data layer has to take its connections from the process's one " +
            "source, which the claim of the queue will draw from too");
    }

    /// <remarks>
    /// Mutation it must fail on: a claim that opens a connection of its own from the
    /// connection string instead of drawing it from the process's source.
    /// </remarks>
    [Fact]
    public async Task FeedTheClaimOfTheNotificationQueue()
    {
        await using var provider = Compose("DM.Workers.NotificationDispatcher");
        var worker = provider.GetServices<IHostedService>()
            .OfType<QueueWorker>()
            .Should().ContainSingle(candidate => candidate.Queue == QueueNames.Notifications,
                "the notification worker drains the notification queue straight from the table")
            .Subject;

        await provider.GetRequiredService<NpgsqlDataSource>().DisposeAsync();

        var claiming = () => worker.HandleNextAsync(CancellationToken.None);
        await claiming.Should().ThrowAsync<ObjectDisposedException>(
            "the claim holds a connection of its own, and it has to come from the process's " +
            "one source, as the data layer's do");
    }

    /// <remarks>
    /// Mutations it must fail on: no loop over the mail queue, a claim on a pool of its own.
    /// </remarks>
    [Fact]
    public async Task FeedTheClaimOfTheMailQueue()
    {
        await using var provider = Compose("DM.Workers.Mail");
        var worker = provider.GetServices<IHostedService>()
            .OfType<QueueWorker>()
            .Should().ContainSingle(candidate => candidate.Queue == QueueNames.Mail,
                "the mail worker drains the mail queue straight from the table")
            .Subject;

        await provider.GetRequiredService<NpgsqlDataSource>().DisposeAsync();

        var claiming = () => worker.HandleNextAsync(CancellationToken.None);
        await claiming.Should().ThrowAsync<ObjectDisposedException>(
            "the mail worker holds one connection source, and its claim draws from it");
    }

    /// <remarks>
    /// Mutations it must fail on: the broker check left in place, a check on a pool of its own.
    /// </remarks>
    [Fact]
    public async Task FeedTheDatabaseCheckOfTheMailWorker()
    {
        await using var provider = Compose("DM.Workers.Mail");

        await provider.GetRequiredService<NpgsqlDataSource>().DisposeAsync();

        var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();
        report.Entries.Keys.Should().Equal(["postgresql"],
            "the mail worker depends on the database alone now that letters wait in its table");
        report.Entries["postgresql"].Exception.Should().BeOfType<ObjectDisposedException>(
            "the check has to probe the source the claim draws from");
    }

    /// <remarks>
    /// Mutations it must fail on: no database check, a check on a pool of its own, a check of
    /// anything the worker no longer depends on.
    /// </remarks>
    [Fact]
    public async Task FeedTheDatabaseCheckOfTheNotificationWorker()
    {
        await using var provider = Compose("DM.Workers.NotificationDispatcher");

        await provider.GetRequiredService<NpgsqlDataSource>().DisposeAsync();

        var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync();
        report.Entries.Keys.Should().Equal(["postgresql"],
            "the notification worker depends on the database alone now that events wait in its table");
        report.Entries["postgresql"].Exception.Should().BeOfType<ObjectDisposedException>(
            "the check has to probe the source the claim and the data layer draw from");
    }

    /// <remarks>Mutation it must fail on: the broker subscription of the mail worker left in place.</remarks>
    [Fact]
    public async Task RunNothingButTheMailQueueInTheMailWorker()
    {
        await using var provider = Compose("DM.Workers.Mail");

        provider.GetServices<IHostedService>()
            .Where(service => service.GetType().Assembly == typeof(DM.Workers.Mail.Startup).Assembly ||
                              service.GetType().Namespace?.StartsWith("DM.Infrastructure.Messaging", StringComparison.Ordinal) == true)
            .Select(service => service.GetType().Name)
            .Should().Equal([nameof(QueueWorker)],
                "no letter passes through the broker, so the worker subscribes to nothing there");
    }

    /// <remarks>
    /// The notification worker's letters take the same road as the API's: the one mail sender,
    /// whose insert draws from the process's source. Mutation it must fail on: a sender that
    /// publishes to the broker or opens a pool of its own.
    /// </remarks>
    [Theory]
    [InlineData("DM.Web.API")]
    [InlineData("DM.Workers.NotificationDispatcher")]
    public async Task FeedTheMailSenderOf(string host)
    {
        await using var provider = Compose(host);
        await using var scope = provider.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<IMailSender>();

        await provider.GetRequiredService<NpgsqlDataSource>().DisposeAsync();

        var sending = () => sender.SendAsync(new EmailLetter
        {
            Address = "player@example.com",
            Subject = "New answer",
            Body = "<p>Hello</p>",
        });
        await sending.Should().ThrowAsync<ObjectDisposedException>(
            "a letter is a row of the mail queue, stored through the process's one source");
    }

    private static ServiceProvider Compose(string host)
    {
        var hosts = CompositionRootShould.BuildHosts();
        foreach (var (_, _, other) in hosts.Where(composed => composed.Item1 != host))
        {
            other.Dispose();
        }

        return hosts.Single(composed => composed.Item1 == host).Item3;
    }

    private static Action DataLayerOpening(IServiceProvider provider) => () =>
    {
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<DmDbContext>().Database.OpenConnection();
    };
}
