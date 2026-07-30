using DM.Infrastructure.Mail;
using DM.Infrastructure.Messaging;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Rabbit.Consuming;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using DM.Domain.Core.Mail;
namespace DM.Workers.Mail;

internal class MailConsumer : BackgroundService
{
    private const string ConsumerExchangeName = "dm.mail.sending";
    private const string DeadLetterExchangeName = "dm.mail.unsent";

    private readonly ILogger<MailConsumer> _logger;
    private readonly IConsumerBuilder _consumerBuilder;
    private readonly IAsyncConnectionFactory _rabbitConnectionFactory;
    private readonly RetryPolicy _consumeRetryPolicy;

    public MailConsumer(
        ILogger<MailConsumer> logger,
        IConsumerBuilder consumerBuilder,
        IAsyncConnectionFactory rabbitConnectionFactory)
    {
        _logger = logger;
        _consumerBuilder = consumerBuilder;
        _rabbitConnectionFactory = rabbitConnectionFactory;

        _consumeRetryPolicy = Policy.Handle<Exception>().WaitAndRetry(5,
            attempt => TimeSpan.FromSeconds(1 << attempt),
            (exception, _) => _logger.LogWarning(exception, "Could not subscribe to the queue"));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("[🚴] Starting mail sending consumer");

        DeadLetterQueue.DeclareTerminal(_rabbitConnectionFactory, DeadLetterExchangeName);

        var parameters = new RabbitConsumerParameters("dm.mail.sender", "dm.mail.sending", ProcessingOrder.Sequential)
        {
            ExchangeName = ConsumerExchangeName,
            RoutingKeys = new[] { "#" },

            // Without this the working queue has no dead-letter exchange, so a
            // letter that keeps failing is rejected into nothing: the retries run
            // out, the exception escapes, and the message is gone with no trace.
            // The exchange and its queue existed all along — only the source was
            // never attached to them.
            DeadLetterExchange = DeadLetterExchangeName,
        };
        var consumer = _consumerBuilder.BuildRabbit<EmailLetter, MailSendingProcessor>(parameters);
        _consumeRetryPolicy.Execute(consumer.Subscribe);

        _logger.LogDebug("[👂] Mail sending consumer is listening to {QueueName} queue", parameters.QueueName);
        return Task.CompletedTask;
    }
}