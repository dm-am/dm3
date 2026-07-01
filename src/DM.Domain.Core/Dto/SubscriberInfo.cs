using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Dto;

/// <summary>
/// Minimal subscriber info for profile display.
/// Distinct from GeneralUser to keep the per-subscriber payload small
/// (a profile may list up to 20 subscribers — we don't need their full
/// rating/picture/stats, just enough to render a styled link).
/// </summary>
public class SubscriberInfo
{
    /// <summary>
    /// Subscriber's username (display name).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Last activity moment (UTC). Used by the profile UI to render
    /// inactive subscribers in muted gray. Null = never recorded.
    /// </summary>
    public DateTimeOffset? LastActivityUtc { get; set; }

    /// <summary>
    /// Subscription settings (channel + per-category author event bits).
    /// Surfaced so the profile UI can filter subscribers per active tab
    /// — e.g. on the Games tab show only subscribers whose Settings has
    /// the AuthorGameEvents bit set.
    /// </summary>
    public SubscriptionSettings Settings { get; set; }
}
