#nullable enable
using System;
using System.ComponentModel.DataAnnotations;

namespace DM.Infrastructure.Persistence.Entities.CrossDomain;

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

    /// <summary>
    /// Number of processing attempts
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Next retry time (null for immediate retry)
    /// </summary>
    public DateTimeOffset? NextRetryUtc { get; set; }

    /// <summary>
    /// Last error message (truncated to 2000 chars)
    /// </summary>
    [MaxLength(2000)]
    public string? LastError { get; set; }
}
