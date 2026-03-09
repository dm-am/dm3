using System;
using System.Threading;
using System.Threading.Tasks;
using Jamq.Client.Abstractions.Consuming;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace DM.Workers.SearchIndexer;

internal class SearchConsumerRetryMiddleware : IConsumerMiddleware
{
    private readonly AsyncRetryPolicy _retryPolicy;

    public SearchConsumerRetryMiddleware(
        ILogger<SearchConsumerRetryMiddleware> logger)
    {
        _retryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(5,
            attempt => TimeSpan.FromSeconds(1 << attempt),
            (exception, _) => logger.LogWarning(exception, "Error indexing search documents"));
    }

    public Task<ProcessResult> InvokeAsync(
        ConsumerContext context,
        ConsumerDelegate next,
        CancellationToken cancellationToken) =>
        _retryPolicy.ExecuteAsync(() => next.Invoke(context, cancellationToken));
}
