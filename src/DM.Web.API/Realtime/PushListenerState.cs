using System.Threading;
using System.Threading.Tasks;
using DM.Infrastructure.Core.Queues;
using DM.Infrastructure.Core.Tracing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DM.Web.API.Realtime;

/// <summary>
/// Whether the listener of this instance is listening on the push channel now, and the
/// health check that answers with it.
/// </summary>
/// <remarks>
/// A state, not an event: cleared the moment the connection is lost. Degraded and not
/// Unhealthy, and without the "ready" tag: requests are served without push, so the
/// instance stays in rotation. The same state feeds the gauge the push alert reads.
/// </remarks>
internal sealed class PushListenerState : IHealthCheck
{
    private volatile bool _listening;

    public PushListenerState() => MessagingMetrics.PushListening.Record(0);

    /// <summary>Records that the listener holds its connection and listens.</summary>
    internal void MarkListening() => Set(true);

    /// <summary>Records that the listener lost its connection or never had one.</summary>
    internal void MarkNotListening() => Set(false);

    private void Set(bool listening)
    {
        _listening = listening;
        MessagingMetrics.PushListening.Record(listening ? 1 : 0);
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(_listening
            ? HealthCheckResult.Healthy(
                $"This instance listens on {QueueNames.PushChannel}.")
            : HealthCheckResult.Degraded(
                $"This instance is not listening on {QueueNames.PushChannel} now: realtime push " +
                "does not reach its tabs. The log of the listener says whether it is reconnecting."));
}
