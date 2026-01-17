#nullable enable
using System;

namespace DM.Services.DataAccess.BusinessObjects.Common;

/// <summary>
/// DAL model for outbox event
/// </summary>
public class OutboxEvent
{
    /// <summary>
    /// Event identifier
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Aggregate identifier
    /// </summary>
    public Guid AggregateId { get; set; }

    /// <summary>
    /// Event type
    /// </summary>
    public int EventType { get; set; }

    /// <summary>
    /// Event payload (JSON)
    /// </summary>
    public string? Payload { get; set; }

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Processing moment
    /// </summary>
    public DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>
    /// Whether the event has been processed
    /// </summary>
    public bool IsProcessed { get; set; }
}
