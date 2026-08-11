using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Personal.Notifications;

/// <inheritdoc />
internal class NotificationApiService : INotificationApiService
{
    private readonly INotificationService _notificationService;
    private readonly IBotLinkService _botLinkService;

    /// <inheritdoc />
    public NotificationApiService(
        INotificationService notificationService,
        IBotLinkService botLinkService)
    {
        _notificationService = notificationService;
        _botLinkService = botLinkService;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Notification>> GetNotifications(PagingQuery query)
    {
        var (notifications, paging) = await _notificationService.GetAsync(query);
        var mapped = notifications.Select(n => new Notification
        {
            Id = n.NotificationId,
            EventType = n.EventType,
            Payload = n.Metadata
        });

        return new ListEnvelope<Notification>(mapped, new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<NotificationCount> GetUnreadCount()
    {
        var count = await _notificationService.CountUnreadAsync();
        return new NotificationCount { Count = count };
    }

    /// <inheritdoc />
    public Task MarkAsRead(Guid notificationId) =>
        _notificationService.MarkAsReadAsync(notificationId);

    /// <inheritdoc />
    public Task MarkAsRead() =>
        _notificationService.MarkAllAsReadAsync();

    /// <inheritdoc />
    public async Task<NotificationSettings> GetNotificationSettings() =>
        Map(await _botLinkService.GetChannels());

    /// <inheritdoc />
    public async Task<NotificationSettings> UpdateNotificationSettings(UpdateNotificationSettingsRequest request)
    {
        if (request.Discord != null)
        {
            await _botLinkService.UpdateChannelPreferences(
                "discord", request.Discord.Enabled, request.Discord.EnabledCategories);
        }

        if (request.Telegram != null)
        {
            await _botLinkService.UpdateChannelPreferences(
                "telegram", request.Telegram.Enabled, request.Telegram.EnabledCategories);
        }

        return Map(await _botLinkService.GetChannels());
    }

    private static NotificationSettings Map(BotChannels channels) => new()
    {
        Discord = MapBotConnection(channels.Discord),
        Telegram = MapBotConnection(channels.Telegram)
    };

    private static BotConnection? MapBotConnection(BotChannel? channel) =>
        channel == null
            ? null
            : new BotConnection
            {
                Connected = channel.Connected,
                Enabled = channel.Enabled,
                EnabledCategories = new HashSet<NotificationCategory>(channel.EnabledCategories)
            };

    /// <inheritdoc />
    public async Task<BotLinkResult> ConnectBot(string type)
    {
        var result = await _botLinkService.GenerateLinkCode(type);
        return new BotLinkResult
        {
            Code = result.Code,
            ExpiresUtc = result.ExpiresUtc
        };
    }

    /// <inheritdoc />
    public Task DisconnectBot(string type) =>
        _botLinkService.Disconnect(type);
}
