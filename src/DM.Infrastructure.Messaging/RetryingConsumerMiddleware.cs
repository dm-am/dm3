using System.Threading;
using System.Threading.Tasks;
using Jamq.Client.Abstractions.Consuming;
using Microsoft.Extensions.Logging;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// Retries and counts the messages a worker takes off its queue.
/// </summary>
/// <remarks>
/// Every message passes through here, which is why the counters are written on this
/// side at all: the warning a retry leaves behind was the only trace of a failing
/// consumer, and it reached no dashboard and no rule.
///
/// One type for both workers. Their two middlewares were the same file twice over -
/// the same interface, the same call into <see cref="MeasuredConsumerPipeline"/>, the
/// same remarks - and named a different queue. That is one decision written down
/// twice, and the second copy is what a third worker would have been written from.
/// The API is deliberately outside it: its middleware measures without retrying,
/// which is a different pipeline rather than another copy of this one.
///
/// The constructor is internal because the queue has to be handed in, and the blanket
/// Autofac scan over this assembly registers whatever declares a public one. Such a
/// registration could not be activated - the queue is a string, and nothing in the
/// graph answers for one - and it would sit there under
/// <see cref="IConsumerMiddleware"/> until something enumerated the interface. So the
/// one way to an instance is
/// <see cref="MessageQueuingConfigurationExtensions.AddDmRetryingConsumer"/>, which is
/// where a host names its queue.
/// </remarks>
public sealed class RetryingConsumerMiddleware : IConsumerMiddleware
{
    private readonly MeasuredConsumerPipeline _pipeline;

    internal RetryingConsumerMiddleware(string queue, ILogger<RetryingConsumerMiddleware> logger) =>
        _pipeline = MeasuredConsumerPipeline.Retrying(queue, logger);

    /// <inheritdoc />
    public Task<ProcessResult> InvokeAsync(
        ConsumerContext context,
        ConsumerDelegate next,
        CancellationToken cancellationToken) =>
        _pipeline.InvokeAsync(context, next, cancellationToken);
}
