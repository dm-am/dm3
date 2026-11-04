using System;
using Polly;
using Polly.Retry;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// The schedule every host retries the broker on: five attempts, 2 to 32 seconds apart.
/// </summary>
public static class BrokerRetry
{
    /// <summary>
    /// A policy on the schedule.
    /// </summary>
    /// <param name="onRetry">Called with the exception before every retry.</param>
    /// <param name="retries">Which exceptions are worth another attempt, all of them when null.</param>
    public static AsyncRetryPolicy Create(Action<Exception> onRetry, Func<Exception, bool>? retries = null) =>
        Policy.Handle<Exception>(retries ?? (_ => true))
            .WaitAndRetryAsync(5,
                attempt => TimeSpan.FromSeconds(1 << attempt),
                (exception, _) => onRetry(exception));
}
