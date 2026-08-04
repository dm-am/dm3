using System;
using System.ComponentModel.DataAnnotations;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Personal.Subscriptions;

/// <summary>
/// Subscription DTO for API responses
/// </summary>
public class Subscription
{
    /// <summary>
    /// Subscription identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Target type (Game, Blog, Author, Board, etc.)
    /// </summary>
    public SubscriptionTargetType TargetType { get; set; }

    /// <summary>
    /// Target entity identifier
    /// </summary>
    public Guid TargetId { get; set; }

    /// <summary>
    /// Name of the target (game, blog, topic or user). Null when the target is
    /// gone: the row is still worth showing, because unsubscribing works.
    /// </summary>
    public string? TargetTitle { get; set; }

    /// <summary>
    /// Username of a User target. A profile is addressed by name, and TargetId
    /// is an identifier.
    /// </summary>
    public string? TargetUsername { get; set; }

    /// <summary>
    /// Notification settings flags
    /// </summary>
    public SubscriptionSettings Settings { get; set; }

    /// <summary>
    /// Creation date
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Request to subscribe to a target
/// </summary>
public class SubscribeRequest
{
    /// <summary>
    /// Target type (Game, Blog, Author, etc.)
    /// </summary>
    [Required]
    public SubscriptionTargetType TargetType { get; set; }

    /// <summary>
    /// Target entity identifier
    /// </summary>
    [Required]
    public Guid TargetId { get; set; }

    /// <summary>
    /// Optional notification settings. If not provided, default settings for the target type will be used.
    /// </summary>
    public SubscriptionSettings? Settings { get; set; }
}

/// <summary>
/// Request to update subscription settings
/// </summary>
public class UpdateSubscriptionRequest
{
    /// <summary>
    /// New notification settings
    /// </summary>
    public SubscriptionSettings Settings { get; set; }
}
