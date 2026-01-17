# Outbox Pattern Implementation

This folder contains the implementation of the Outbox pattern for reliable event publishing in the DM3 project.

## Overview

The Outbox pattern ensures reliable event publishing by storing events in the database as part of the same transaction that modifies the business data. A background processor then reads these events and publishes them to the message queue.

## Components

### OutboxEvent Entity
Located in: `DM.Services.DataAccess/BusinessObjects/Common/OutboxEvent.cs`

The database entity that stores events to be published:
- `Id`: Auto-incrementing identifier
- `AggregateId`: The entity identifier associated with the event
- `EventType`: The type of event (from `EventType` enum)
- `Payload`: Optional JSON payload for additional event data
- `CreatedAt`: When the event was created
- `ProcessedAt`: When the event was successfully published
- `IsProcessed`: Whether the event has been published

### OutboxProcessor
Located in: `DM.Services.MessageQueuing/Outbox/OutboxProcessor.cs`

A background service that:
- Runs every 5 seconds
- Queries for unprocessed events
- Publishes them via `IInvokedEventProducer`
- Marks them as processed
- Logs errors without stopping the process

## Usage Example

To use the Outbox pattern in your service:

```csharp
public class MyBusinessService
{
    private readonly DmDbContext dbContext;

    public async Task CreateEntity(Guid entityId)
    {
        // Start a transaction
        using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            // 1. Perform your business logic
            var entity = new MyEntity { Id = entityId, Name = "Test" };
            dbContext.MyEntities.Add(entity);

            // 2. Add outbox event in the same transaction
            var outboxEvent = new OutboxEvent
            {
                AggregateId = entityId,
                EventType = (int)EventType.NewEntity,
                CreatedAt = DateTimeOffset.UtcNow,
                IsProcessed = false,
                Payload = null // Optional: Add JSON payload if needed
            };
            dbContext.OutboxEvents.Add(outboxEvent);

            // 3. Commit both changes atomically
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

## Benefits

1. **Reliability**: Events are guaranteed to be published if the business transaction succeeds
2. **Atomicity**: Event creation and business logic happen in the same database transaction
3. **Fault Tolerance**: Failed event publishing attempts don't affect the business operation
4. **Retry Logic**: The processor automatically retries failed events every 5 seconds
5. **No Distributed Transaction**: Avoids the complexity of 2-phase commits

## Configuration

The OutboxProcessor is automatically registered as an `IHostedService` in the `MessageQueuingModule`. No additional configuration is required.

## Migration

Run the migration to create the OutboxEvents table:

```bash
dotnet ef database update --project src/DM.Services.DataAccess
```

## Monitoring

The OutboxProcessor logs:
- Startup: `[🚴] Starting outbox processor`
- Processing: `Processing {Count} outbox events`
- Errors: `Error processing outbox events` and `Failed to process outbox event {EventId}`

Monitor these logs to ensure events are being processed correctly.
