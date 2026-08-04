using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Subscriptions;

/// <summary>
/// Subscription DTO
/// </summary>
public class Subscription
{
    /// <summary>Subscription identifier</summary>
    public Guid Id { get; set; }

    /// <summary>Subscriber user identifier</summary>
    public Guid SubscriberId { get; set; }

    /// <summary>Target type</summary>
    public SubscriptionTargetType TargetType { get; set; }

    /// <summary>Target entity identifier</summary>
    public Guid TargetId { get; set; }

    /// <summary>
    /// Name of the target entity, when the read resolved it.
    /// </summary>
    /// <remarks>
    /// A subscription row holds a type and a bare identifier, so the page built
    /// out of it could print nothing but the identifier — a list of GUIDs, one
    /// per line. Filled by the reads that answer a person; the fan-out reads
    /// leave it null because notification delivery has no use for it.
    /// </remarks>
    public string? TargetTitle { get; set; }

    /// <summary>
    /// Username of a User target, when the read resolved it. A profile is
    /// addressed by name, and the row carries only the identifier.
    /// </summary>
    public string? TargetUsername { get; set; }

    /// <summary>Notification settings</summary>
    public SubscriptionSettings Settings { get; set; }

    /// <summary>Creation date</summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
