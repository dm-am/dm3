using DM.Infrastructure.Mail;
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

        ConfigureDLX();

        var parameters = new RabbitConsumerParameters("dm.mail.sender", "dm.mail.sending", ProcessingOrder.Sequential)
        {
            ExchangeName = ConsumerExchangeName,
            RoutingKeys = new[] { "#" }
        };
        var consumer = _consumerBuilder.BuildRabbit<MailLetter, MailSendingProcessor>(parameters);
        _consumeRetryPolicy.Execute(consumer.Subscribe);

        _logger.LogDebug("[👂] Mail sending consumer is listening to {QueueName} queue", parameters.QueueName);
        return Task.CompletedTask;
    }

    private void ConfigureDLX()
    {
        var mailDLXQueue = $"{DeadLetterExchangeName}-dlq";
        var mailDLXRetryTimeoutInMs = 60000;

        using var configuringConnection = _rabbitConnectionFactory.CreateConnection();
        using var channel = configuringConnection.CreateModel();

        channel.ExchangeDeclare(DeadLetterExchangeName, ExchangeType.Fanout, true);
        channel.QueueDeclare(mailDLXQueue, true, false, false, new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", ConsumerExchangeName },
            { "x-message-ttl", mailDLXRetryTimeoutInMs }
        });
        channel.QueueBind(mailDLXQueue, DeadLetterExchangeName, string.Empty);
    }
}