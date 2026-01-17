using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.MessageQueuing.GeneralBus;
using DM.Services.Search.Consumer.Implementation;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Rabbit.Consuming;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenSearch.Client;
using Polly;
using Polly.Retry;
using Policy = Polly.Policy;

namespace DM.Services.Search.Consumer;

internal class SearchEngineConsumer : BackgroundService
{
    private readonly ILogger<SearchEngineConsumer> _logger;
    private readonly IOpenSearchClient _elasticClient;
    private readonly IConsumerBuilder _consumerBuilder;
    private readonly RetryPolicy _consumeRetryPolicy;

    public SearchEngineConsumer(
        ILogger<SearchEngineConsumer> logger,
        IOpenSearchClient elasticClient,
        IConsumerBuilder consumerBuilder)
    {
        _logger = logger;
        _elasticClient = elasticClient;
        _consumerBuilder = consumerBuilder;
        _consumeRetryPolicy = Policy.Handle<Exception>().WaitAndRetry(5,
            attempt => TimeSpan.FromSeconds(1 << attempt),
            (exception, _) => _logger.LogWarning(exception, "Could not subscribe to the queue"));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("[🚴] Starting search engine consumer");

        var existsResponse = _elasticClient.Indices.Exists(Configuration.SearchEngineConfiguration.IndexName);
        if (existsResponse is not { IsValid: true, Exists: true })
        {
            _logger.LogDebug("Creating search engine index {IndexName}", Configuration.SearchEngineConfiguration.IndexName);
            var createIndexResponse = _elasticClient.Indices.Create(Configuration.SearchEngineConfiguration.IndexName);
            if (createIndexResponse is not { IsValid: true, Index: Configuration.SearchEngineConfiguration.IndexName })
            {
                _logger.LogError("Could not create search index on consumer start");
            }
        }

        var parameters = new RabbitConsumerParameters("dm.search-engine", "dm.search-engine", ProcessingOrder.Unmanaged)
        {
            ExchangeName = InvokedEventsTransport.ExchangeName,
            RoutingKeys = new[]
            {
                EventType.ActivatedUser,
                EventType.NewForumComment,
                EventType.ChangedForumComment,
                EventType.DeletedForumComment,
                EventType.NewForumTopic,
                EventType.ChangedForumTopic,
                EventType.DeletedForumTopic,
            }.ToRoutingKeys(),
        };
        var consumer = _consumerBuilder.BuildRabbit<InvokedEvent, CompositeIndexer>(parameters);
        _consumeRetryPolicy.Execute(consumer.Subscribe);

        _logger.LogDebug("[👂] Search engine consumer is listening to {QueueName} queue", parameters.QueueName);
        return Task.CompletedTask;
    }
}