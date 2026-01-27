using System;
using System.Threading;
using System.Threading.Tasks;
using Jamq.Client.Abstractions.Consuming;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace DM.Services.Notifications.Consumer;

internal class NotificationConsumerRetryMiddleware : IConsumerMiddleware
{
    private readonly AsyncRetryPolicy _retryPolicy;

    public NotificationConsumerRetryMiddleware(
        ILogger<NotificationConsumerRetryMiddleware> logger)
    {
        _retryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(5,
            attempt => TimeSpan.FromSeconds(1 << attempt),
            (exception, _) => logger.LogWarning(exception, "Error processing notifications"));
    }

    public Task<ProcessResult> InvokeAsync(
        ConsumerContext context,
        ConsumerDelegate next,
        CancellationToken cancellationToken) =>
        _retryPolicy.ExecuteAsync(() => next.Invoke(context, cancellationToken));
}
