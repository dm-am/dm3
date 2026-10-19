using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DM.Web.API.Realtime;

/// <summary>
/// Whether this process ever opened its subscription to the realtime push queue,
/// and the health check that answers with it.
/// </summary>
/// <remarks>
/// An event, not a state: Healthy means the consumer got past its start, and says
/// nothing about the connection now. Inside the host nothing else tells an instance
/// dropping every push from one nobody pushes to. Degraded and not Unhealthy, and
/// without the "ready" tag: requests are served without push, so the instance
/// stays in rotation.
/// </remarks>
internal sealed class RealtimeSubscriptionState : IHealthCheck
{
    private volatile bool _subscribed;

    /// <summary>Records that this host opened its subscription to the push queue.</summary>
    internal void MarkSubscribed() => _subscribed = true;

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(_subscribed
            ? HealthCheckResult.Healthy(
                $"This instance opened its subscription to {RealtimeNotificationConsumer.QueueName}. " +
                "Whether the connection behind it is up now is answered by the broker, not here.")
            : HealthCheckResult.Degraded(
                $"This instance has not opened its subscription to {RealtimeNotificationConsumer.QueueName} " +
                "yet: realtime push is dropped at the broker. The log of the consumer says whether the " +
                "subscription is still being attempted."));
}
