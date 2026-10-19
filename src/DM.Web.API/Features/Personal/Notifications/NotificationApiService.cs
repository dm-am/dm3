using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Personal.Notifications;

public class NotificationApiService
{
    private readonly INotificationService _notificationService;
    private readonly IBotLinkService _botLinkService;

    public NotificationApiService(
        INotificationService notificationService,
        IBotLinkService botLinkService)
    {
        _notificationService = notificationService;
        _botLinkService = botLinkService;
    }

    public async Task<ListEnvelope<Notification>> GetNotifications(PagingQuery query)
    {
        var (notifications, paging) = await _notificationService.GetAsync(query);
        var mapped = notifications.Select(NotificationMapper.ToNotification);

        return new ListEnvelope<Notification>(mapped, new PagingInfo(paging));
    }

    public async Task<UnreadCount> GetUnreadCount() =>
        new() { Count = await _notificationService.CountUnreadAsync() };

    public Task MarkAsRead(Guid notificationId) =>
        _notificationService.MarkAsReadAsync(notificationId);

    public Task MarkAllAsRead() =>
        _notificationService.MarkAllAsReadAsync();

    public async Task<NotificationSettings> GetNotificationSettings() =>
        Map(await _botLinkService.GetChannels());

    public async Task<NotificationSettings> UpdateNotificationSettings(UpdateNotificationSettingsRequest request)
    {
        foreach (var (type, channel) in new[] { ("discord", request.Discord), ("telegram", request.Telegram) })
        {
            if (channel != null)
            {
                await _botLinkService.UpdateChannelPreferences(type, channel.Enabled, channel.EnabledCategories);
            }
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

    public async Task<BotLinkResult> ConnectBot(string type)
    {
        var result = await _botLinkService.GenerateLinkCode(type);
        return new BotLinkResult
        {
            Code = result.Code,
            ExpiresUtc = result.ExpiresUtc
        };
    }

    public Task DisconnectBot(string type) =>
        _botLinkService.Disconnect(type);
}
