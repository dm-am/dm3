using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Workers.SearchIndexer.Implementation;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Rabbit.Consuming;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenSearch.Client;
using Polly;
using Polly.Retry;
using Policy = Polly.Policy;

namespace DM.Workers.SearchIndexer;

internal class SearchIndexerConsumer : BackgroundService
{
    private readonly ILogger<SearchIndexerConsumer> _logger;
    private readonly IOpenSearchClient _elasticClient;
    private readonly IConsumerBuilder _consumerBuilder;
    private readonly RetryPolicy _consumeRetryPolicy;

    public SearchIndexerConsumer(
        ILogger<SearchIndexerConsumer> logger,
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
        _logger.LogDebug("[??] Starting search engine consumer");

        var existsResponse = _elasticClient.Indices.Exists(DM.Domain.Core.Search.SearchEngineConfiguration.IndexName);
        if (existsResponse is not { IsValid: true, Exists: true })
        {
            _logger.LogDebug("Creating search engine index {IndexName}", DM.Domain.Core.Search.SearchEngineConfiguration.IndexName);
            var createIndexResponse = _elasticClient.Indices.Create(DM.Domain.Core.Search.SearchEngineConfiguration.IndexName);
            if (createIndexResponse is not { IsValid: true, Index: DM.Domain.Core.Search.SearchEngineConfiguration.IndexName })
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
                EventType.NewTopicComment,
                EventType.ChangedTopicComment,
                EventType.DeletedTopicComment,
                EventType.NewTopic,
                EventType.ChangedTopic,
                EventType.DeletedTopic,
            }.ToRoutingKeys(),
        };
        var consumer = _consumerBuilder.BuildRabbit<InvokedEvent, CompositeIndexer>(parameters);
        _consumeRetryPolicy.Execute(consumer.Subscribe);

        _logger.LogDebug("[??] Search engine consumer is listening to {QueueName} queue", parameters.QueueName);
        return Task.CompletedTask;
    }
}