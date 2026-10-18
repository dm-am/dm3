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
    /// Status of a Game or Blog target: the page names it in the colour of its
    /// status, as the site names games and blogs everywhere. Null for a topic,
    /// a user and a target that is gone.
    /// </summary>
    public ModuleStatus? TargetStatus { get; set; }

    /// <summary>
    /// When a Game or Blog target was first activated, what a "new" game or blog
    /// is counted from (the same date as activatedUtc of GameRef and BlogRef).
    /// Null for a topic, a user, a target that is gone and one never activated.
    /// </summary>
    public DateTimeOffset? TargetActivatedUtc { get; set; }

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
