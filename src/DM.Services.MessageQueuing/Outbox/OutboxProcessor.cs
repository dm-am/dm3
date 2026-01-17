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

namespace DM.Services.MessageQueuing.Outbox;

/// <summary>
/// Background service for processing outbox events
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<OutboxProcessor> logger;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="serviceProvider">Service provider</param>
    /// <param name="logger">Logger</param>
    public OutboxProcessor(IServiceProvider serviceProvider, ILogger<OutboxProcessor> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
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

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessOutboxEvents(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var producer = scope.ServiceProvider.GetRequiredService<IInvokedEventProducer>();

        var pendingEvents = await dbContext.OutboxEvents
            .Where(e => !e.IsProcessed)
            .OrderBy(e => e.Id)
            .Take(100)
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
                evt.IsProcessed = true;
                evt.ProcessedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process outbox event {EventId}", evt.Id);
            }
        }
    }
}
