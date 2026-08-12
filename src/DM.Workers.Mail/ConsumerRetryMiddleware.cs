using System.Threading;
using System.Threading.Tasks;
using DM.Infrastructure.Messaging;
using Jamq.Client.Abstractions.Consuming;
using Microsoft.Extensions.Logging;

namespace DM.Workers.Mail;

/// <summary>
/// Retries and counts the letters this worker takes off its queue.
/// </summary>
/// <remarks>
/// Every letter passes through here, which is why the counters are written on
/// this side at all: the warning a retry leaves behind was the only trace of a
/// failing sender, and it reached no dashboard and no rule.
///
/// The policy and the instruments live in <see cref="MeasuredConsumerPipeline"/>,
/// one copy for every host that consumes. What is left here is what belongs to
/// this host and to no other: the queue it reads, and the logger the retries are
/// written to.
/// </remarks>
internal class ConsumerRetryMiddleware : IConsumerMiddleware
{
    /// <summary>Queue this middleware sits in front of, as the metrics label it.</summary>
    private const string Queue = "dm.mail.sending";

    private readonly MeasuredConsumerPipeline _pipeline;

    public ConsumerRetryMiddleware(
        ILogger<ConsumerRetryMiddleware> logger) =>
        _pipeline = MeasuredConsumerPipeline.Retrying(Queue, logger);

    /// <inheritdoc />
    public Task<ProcessResult> InvokeAsync(
        ConsumerContext context,
        ConsumerDelegate next,
        CancellationToken cancellationToken) =>
        _pipeline.InvokeAsync(context, next, cancellationToken);
}
