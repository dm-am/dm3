using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess;
using DM.Services.MessageQueuing.GeneralBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Services.MessageQueuing.Outbox;

/// <summary>
/// Background service for processing outbox events
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<OutboxProcessor> logger;
    private readonly OutboxConfiguration configuration;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    /// <param name="logger">Logger</param>
    /// <param name="options">Outbox configuration</param>
    public OutboxProcessor(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessor> logger,
        IOptions<OutboxConfiguration> options)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        configuration = options.Value;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogDebug("[🚴] Starting outbox processor");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxEvents(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing outbox events");
            }

            await Task.Delay(TimeSpan.FromSeconds(configuration.PollingIntervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessOutboxEvents(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var producer = scope.ServiceProvider.GetRequiredService<IInvokedEventProducer>();

        var now = DateTimeOffset.UtcNow;

        // Query unprocessed events that are ready for retry (NextRetryUtc is null or in the past)
        var pendingEvents = await dbContext.OutboxEvents
            .Where(e => !e.IsProcessed && (e.NextRetryUtc == null || e.NextRetryUtc <= now))
            .OrderBy(e => e.Id)
            .Take(configuration.MaxBatchSize)
            .ToListAsync(ct);

        if (pendingEvents.Count == 0)
        {
            return;
        }

        logger.LogDebug("Processing {Count} outbox events", pendingEvents.Count);

        foreach (var evt in pendingEvents)
        {
            try
            {
                await producer.Send((EventType)evt.EventType, evt.AggregateId);

                // Success: mark as processed
                evt.IsProcessed = true;
                evt.ProcessedAt = now;
                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                // Failure: increment retry count and schedule next retry
                evt.RetryCount++;
                evt.LastError = ex.Message.Length > 2000
                    ? ex.Message.Substring(0, 2000)
                    : ex.Message;

                if (evt.RetryCount >= configuration.MaxRetries)
                {
                    // Dead-letter: stop retrying after max attempts
                    evt.IsProcessed = true;
                    logger.LogWarning(
                        "Outbox event {EventId} dead-lettered after {RetryCount} attempts. Last error: {Error}",
                        evt.Id, evt.RetryCount, evt.LastError);
                }
                else
                {
                    // Calculate exponential backoff: 2^RetryCount * BaseRetryDelaySeconds
                    var delaySeconds = Math.Pow(2, evt.RetryCount) * configuration.BaseRetryDelaySeconds;
                    evt.NextRetryUtc = now.AddSeconds(delaySeconds);

                    logger.LogWarning(
                        "Outbox event {EventId} failed (attempt {RetryCount}/{MaxRetries}). Next retry at {NextRetry}. Error: {Error}",
                        evt.Id, evt.RetryCount, configuration.MaxRetries, evt.NextRetryUtc, evt.LastError);
                }

                // Save retry state (important: don't lose this on failure)
                try
                {
                    await dbContext.SaveChangesAsync(ct);
                }
                catch (Exception saveEx)
                {
                    logger.LogError(saveEx, "Failed to save retry state for outbox event {EventId}", evt.Id);
                }
            }
        }
    }
}
