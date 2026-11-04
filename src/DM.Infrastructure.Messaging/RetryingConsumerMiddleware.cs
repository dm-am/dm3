using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// Retries and counts the messages a worker takes off its queue.
/// </summary>
/// <remarks>
/// One type for every worker; the queue comes in through <see cref="MessagingConfigurationExtensions.AddDmRetryingConsumer"/>.
/// </remarks>
public sealed class RetryingConsumerMiddleware : IConsumerMiddleware
{
    private readonly MeasuredConsumerPipeline _pipeline;

    internal RetryingConsumerMiddleware(string queue, ILogger<RetryingConsumerMiddleware> logger) =>
        _pipeline = MeasuredConsumerPipeline.Retrying(queue, logger);

    /// <inheritdoc />
    public Task<ProcessResult> InvokeAsync(ConsumerDelegate next, CancellationToken cancellationToken) =>
        _pipeline.InvokeAsync(next, cancellationToken);
}
