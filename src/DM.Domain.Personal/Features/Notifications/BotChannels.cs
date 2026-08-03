using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// How one bot channel delivers notifications.
/// </summary>
/// <param name="Enabled">Whether the channel delivers at all.</param>
/// <param name="EnabledCategories">Categories the channel delivers.</param>
public record ChannelPreferences(
    bool Enabled,
    IReadOnlyCollection<NotificationCategory> EnabledCategories);

/// <summary>
/// Preferences of both bot channels of a user. A channel that is not connected has
/// none: they are written when it is linked and cleared when it is disconnected.
/// </summary>
/// <param name="Discord">Discord preferences, null when the channel is not connected.</param>
/// <param name="Telegram">Telegram preferences, null when the channel is not connected.</param>
public record BotChannelPreferences(
    ChannelPreferences? Discord,
    ChannelPreferences? Telegram);

/// <summary>
/// A bot channel as its owner sees it: linked or not, and how it delivers when it is.
/// </summary>
/// <param name="Connected">Whether an external account is linked.</param>
/// <param name="Enabled">Whether the channel delivers notifications.</param>
/// <param name="EnabledCategories">Categories the channel delivers.</param>
public record BotChannel(
    bool Connected,
    bool Enabled,
    IReadOnlyCollection<NotificationCategory> EnabledCategories);

/// <summary>
/// Both bot channels of the current user. A channel that is neither linked nor
/// configured is null, because there is nothing about it to show.
/// </summary>
/// <param name="Discord">Discord channel, null when there is nothing to show.</param>
/// <param name="Telegram">Telegram channel, null when there is nothing to show.</param>
public record BotChannels(BotChannel? Discord, BotChannel? Telegram);
