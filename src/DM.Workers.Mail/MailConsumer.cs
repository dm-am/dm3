using DM.Domain.Core.Mail;
using DM.Infrastructure.Mail;
using DM.Infrastructure.Messaging;
using DM.Workers.Mail.Sending;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly.Retry;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Workers.Mail;

internal class MailConsumer : BackgroundService
{
    /// <summary>Queue this worker reads, as both the topology and the metrics name it.</summary>
    internal const string QueueName = "dm.mail.sending";

    // The producer's constant: a one-sided rename would drop every letter silently.
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
        _consumeRetryPolicy = BrokerRetry.Create(exception =>
            _logger.LogWarning(exception, "Could not subscribe to the queue"));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("[🚴] Starting mail sending consumer");

        // Everything before the first await runs inside host startup.
        await Task.Yield();

        var parameters = new DmConsumerParameters("dm.mail.sender", QueueName)
        {
            ExchangeName = ConsumerExchangeName,
            DeadLetterExchange = DeadLetterExchangeName,
        };
        var consumer = _consumerBuilder.Build<EmailLetter, MailSendingProcessor>(parameters);

        // The dead-letter declaration talks to the same broker, so it retries with the subscription.
        await _consumeRetryPolicy.ExecuteAsync(async token =>
        {
            await DeadLetterQueue.DeclareTerminal(_brokerConnection, DeadLetterExchangeName, token);
            await consumer.Subscribe(token);
        }, stoppingToken);

        _logger.LogDebug("[👂] Mail sending consumer is listening to {QueueName} queue", parameters.QueueName);
    }
}
