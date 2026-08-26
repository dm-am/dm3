using DM.Domain.Core.Mail;
using DM.Infrastructure.Mail;
using DM.Infrastructure.Messaging;
using DM.Workers.Mail.Sending;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Workers.Mail;

internal class MailConsumer : BackgroundService
{
    /// <summary>Queue this worker reads, as both the topology and the metrics name it.</summary>
    internal const string QueueName = "dm.mail.sending";

    // The producer's name, not a copy of it. Two literals were kept here on the
    // theory that they protect the route from a one-sided rename; they do the
    // opposite - a rename of either was silent, and a letter published to an
    // exchange nothing is bound to is dropped by the broker without a trace. One
    // constant moves both ends at once. The queue above keeps a literal of its
    // own because it is a different thing that happens to read the same.
    private const string ConsumerExchangeName = MailTransport.ExchangeName;

    private const string DeadLetterExchangeName = "dm.mail.unsent";

    private readonly ILogger<MailConsumer> _logger;
    private readonly IDmConsumerBuilder _consumerBuilder;
    private readonly DmBrokerConnection _brokerConnection;
    private readonly AsyncRetryPolicy _consumeRetryPolicy;

    public MailConsumer(
        ILogger<MailConsumer> logger,
        IDmConsumerBuilder consumerBuilder,
        DmBrokerConnection brokerConnection)
    {
        _logger = logger;
        _consumerBuilder = consumerBuilder;
        _brokerConnection = brokerConnection;

        _consumeRetryPolicy = Policy.Handle<Exception>().WaitAndRetryAsync(5,
            attempt => TimeSpan.FromSeconds(1 << attempt),
            (exception, _) => _logger.LogWarning(exception, "Could not subscribe to the queue"));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("[🚴] Starting mail sending consumer");

        // Yield before touching the broker: everything before the first await runs
        // inside host startup, so a broker that is not up yet aborted the host before
        // its own health check could report why, and the retry below held the start
        // for a minute of Thread.Sleep first. The API consumer next door has done it
        // this way all along.
        await Task.Yield();

        var parameters = new DmConsumerParameters("dm.mail.sender", QueueName)
        {
            ExchangeName = ConsumerExchangeName,
            RoutingKeys = ["#"],

            // Without this the working queue has no dead-letter exchange, so a
            // letter that keeps failing is rejected into nothing: the retries run
            // out, the exception escapes, and the message is gone with no trace.
            // The exchange and its queue existed all along — only the source was
            // never attached to them.
            DeadLetterExchange = DeadLetterExchangeName,
        };
        var consumer = _consumerBuilder.Build<EmailLetter, MailSendingProcessor>(parameters);

        // The dead-letter declaration is inside the policy with the subscription:
        // both talk to the same broker, and one call left outside the policy is
        // the one an unreachable broker throws past entirely.
        await _consumeRetryPolicy.ExecuteAsync(async token =>
        {
            await DeadLetterQueue.DeclareTerminal(_brokerConnection, DeadLetterExchangeName, token);
            await consumer.Subscribe(token);
        }, stoppingToken);

        _logger.LogDebug("[👂] Mail sending consumer is listening to {QueueName} queue", parameters.QueueName);
    }
}
