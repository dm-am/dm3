using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DM.Web.API.Realtime;

/// <summary>
/// Whether this process ever opened its subscription to the realtime push queue,
/// and the health check that answers with it.
/// </summary>
/// <remarks>
/// What is recorded is an event and not a state: the subscription opened once,
/// here, in this process. Nothing takes it back, so Healthy is the claim "the
/// consumer got past its start", and it stays true afterwards whatever becomes of
/// the connection. Degraded is the stronger of the two answers, because it is the
/// one that cannot be wrong: a subscription that has not opened yet is one no push
/// is reaching, and no push ever will until an attempt succeeds.
///
/// It is the only thing inside the host that knows even that much. A subscription
/// that never opened leaves no queue on the broker at all, the queue being
/// exclusive to the connection of the instance, so from the outside the failure is
/// an absent series, and the rule that catches it (RealtimePushWithoutConsumer)
/// needs both the API and the broker scraped before it can fire. Meanwhile the
/// host serves every request and its liveness probe answers, so nothing else
/// distinguishes an instance dropping every push from an instance nobody is
/// pushing to.
///
/// State and check in one type, because the state has exactly one reader: a
/// second class would only be the same boolean read through an interface.
///
/// Degraded and not Unhealthy, and registered without the "ready" tag: every
/// request is served without push, so an instance in this state must stay in
/// rotation. That is the same call the broker check makes next door, and for the
/// same reason.
///
/// What Healthy does not answer: whether the subscription is open now. A
/// connection that drops afterwards leaves this flag where it is - the client
/// redials and re-declares the topology and the subscriptions with it (see
/// DmBrokerConnection), and while it is down the queue is gone from the broker,
/// which is the case the alert above is shaped for.
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
