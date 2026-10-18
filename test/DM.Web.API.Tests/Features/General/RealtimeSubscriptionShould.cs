using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Messaging;
using DM.Testing;
using DM.Web.API.Realtime;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DM.Web.API.Tests.Features.General;

/// <summary>
/// An instance either has the push queue subscribed or says that it has not.
/// </summary>
/// <remarks>
/// The subscription used to be attempted five times over 62 seconds and then
/// abandoned, which is shorter than a broker restart: an API that came up while
/// RabbitMQ was still starting served every request with push silently off for
/// the life of the process. Nothing inside the host reported it, and the rule that
/// would have (RealtimePushWithoutConsumer) needs the broker scraped and five
/// minutes, over a queue that an unsubscribed consumer never declares.
///
/// Driven through the real background service with the wait injected as zero: what
/// is asserted is the loop and the state it leaves behind, and a test that spent
/// the ladder in real time would assert the same thing an hour later.
/// </remarks>
public class RealtimeSubscriptionShould : UnitTestBase
{
    /// <summary>
    /// Failures before the subscription opens. Above any finite ladder this
    /// consumer could carry (the five it used to), so passing means the loop does
    /// not run out rather than that the outage was short enough.
    /// </summary>
    private const int FailuresBeforeSuccess = 7;

    /// <summary>Every wait in this test is a timeout, so a stuck loop fails.</summary>
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private readonly IDmConsumer _consumer = Mock<IDmConsumer>();
    private readonly IDmConsumerBuilder _consumerBuilder = Mock<IDmConsumerBuilder>();
    private readonly RealtimeSubscriptionState _subscriptionState = new();
    private int _attempts;

    public RealtimeSubscriptionShould() =>
        _consumerBuilder
            .Build<RealtimeNotification, RealtimeNotificationProcessor>(Arg.Any<DmConsumerParameters>())
            .Returns(_consumer);

    [Fact]
    public async Task KeepAttemptingTheSubscriptionUntilItOpens()
    {
        FailThenSucceed();

        await Run();

        _attempts.Should().Be(FailuresBeforeSuccess + 1,
            "a broker that refuses more times than the ladder had attempts left the queue " +
            "unsubscribed for the life of the process");
        (await Report()).Status.Should().Be(HealthStatus.Healthy,
            "the subscription opened, and the check exists to say which of the two states " +
            "this instance is in");
    }

    [Fact]
    public async Task ReportDegradedWhileTheQueueIsNotSubscribed()
    {
        FailThenSucceed();

        var beforeSubscribing = await Report();

        beforeSubscribing.Status.Should().Be(HealthStatus.Degraded,
            "an instance dropping every push answers its liveness probe and serves every " +
            "request, so the detail report is the only place the difference shows");
        beforeSubscribing.Description.Should().Contain(RealtimeNotificationConsumer.QueueName,
            "a degraded check whose reason names no queue sends the reader back to the code");

        await Run();

        (await Report()).Status.Should().Be(HealthStatus.Healthy);
    }

    /// <summary>
    /// A stopping host is not waited on by the retries.
    /// </summary>
    /// <remarks>
    /// The loop is unbounded now, so the token is the only thing that ends it: a
    /// retry that does not observe cancellation would hold the shutdown for as
    /// long as the broker stays down, which is the failure the previous finite
    /// ladder at least ended on its own.
    /// </remarks>
    [Fact]
    public async Task StopAttemptingWhenTheHostStops()
    {
        using var stopping = new CancellationTokenSource(TestTimeout);
        _consumer.Subscribe(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            _attempts++;
            if (_attempts == 3)
            {
                stopping.Cancel();
            }

            return Task.FromException(new InvalidOperationException("the broker is down"));
        });

        await Run(stopping.Token);

        _attempts.Should().Be(3,
            "the third attempt cancelled the token, and a fourth would be the loop opening " +
            "one more subscription for a host that is already going down");
        (await Report()).Status.Should().Be(HealthStatus.Degraded,
            "a host that stopped before subscribing never had the queue");
    }

    /// <summary>Fails the subscription a fixed number of times, then opens it.</summary>
    private void FailThenSucceed() =>
        _consumer.Subscribe(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            _attempts++;
            return _attempts <= FailuresBeforeSuccess
                ? Task.FromException(new InvalidOperationException("the broker is down"))
                : Task.CompletedTask;
        });

    /// <summary>Runs the consumer to the end of its execution.</summary>
    private async Task Run(CancellationToken stoppingToken = default)
    {
        using var service = new RealtimeNotificationConsumer(
            NullLogger<RealtimeNotificationConsumer>.Instance,
            _consumerBuilder,
            _subscriptionState,
            _ => TimeSpan.Zero);

        await service.StartAsync(stoppingToken);
        await service.ExecuteTask!.WaitAsync(TestTimeout);
    }

    private Task<HealthCheckResult> Report() =>
        _subscriptionState.CheckHealthAsync(new HealthCheckContext());
}
