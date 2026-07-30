using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Dto;
using DM.Infrastructure.Persistence.Entities.Account.Settings;
using DM.Web.API.Notifications;
using DbUserSettings = DM.Infrastructure.Persistence.Entities.Account.Settings.UserSettings;

namespace DM.Web.API.Features.Personal.Notifications;

/// <inheritdoc />
internal class NotificationApiService : INotificationApiService
{
    private readonly INotificationService _notificationService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IBotLinkRepository _botLinkRepository;
    private readonly INotificationSettingsRepository _settingsRepository;
    private readonly IBotLinkService _botLinkService;

    /// <inheritdoc />
    public NotificationApiService(
        INotificationService notificationService,
        IIdentityProvider identityProvider,
        IBotLinkRepository botLinkRepository,
        INotificationSettingsRepository settingsRepository,
        IBotLinkService botLinkService)
    {
        _notificationService = notificationService;
        _identityProvider = identityProvider;
        _botLinkRepository = botLinkRepository;
        _settingsRepository = settingsRepository;
        _botLinkService = botLinkService;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Notification>> GetNotifications(int skip = 0, int take = 20)
    {
        var query = new PagingQuery { Skip = skip, Take = take };
        var notifications = await _notificationService.GetAsync(query);
        var mapped = notifications.Select(n => new Notification
        {
            Id = n.NotificationId,
            EventType = n.EventType,
            Payload = n.Metadata
        });

        return mapped;
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
    public async Task<NotificationSettings> GetNotificationSettings()
    {
        var userId = _identityProvider.Current.User.UserId;

        var channels = await _botLinkRepository.GetChannelIds(userId);

        var settings = await _settingsRepository.GetByUserId(userId);

        var dto = new NotificationSettings
        {
            Discord = MapBotConnection(channels.DiscordId, settings?.DiscordPreferences),
            Telegram = MapBotConnection(channels.TelegramId, settings?.TelegramPreferences)
        };

        return dto;
    }

    /// <inheritdoc />
    public async Task<NotificationSettings> UpdateNotificationSettings(UpdateNotificationSettingsRequest request)
    {
        var userId = _identityProvider.Current.User.UserId;

        var settings = await _settingsRepository.GetByUserId(userId);
        if (settings == null)
        {
            settings = DbUserSettings.CreateDefault(userId);
        }

        if (request.Discord != null && settings.DiscordPreferences != null)
        {
            if (request.Discord.Enabled.HasValue)
                settings.DiscordPreferences.Enabled = request.Discord.Enabled.Value;
            if (request.Discord.EnabledCategories != null)
                settings.DiscordPreferences.EnabledCategories = request.Discord.EnabledCategories;
        }

        if (request.Telegram != null && settings.TelegramPreferences != null)
        {
            if (request.Telegram.Enabled.HasValue)
                settings.TelegramPreferences.Enabled = request.Telegram.Enabled.Value;
            if (request.Telegram.EnabledCategories != null)
                settings.TelegramPreferences.EnabledCategories = request.Telegram.EnabledCategories;
        }

        await _settingsRepository.Upsert(settings);

        var channels = await _botLinkRepository.GetChannelIds(userId);

        var dto = new NotificationSettings
        {
            Discord = MapBotConnection(channels.DiscordId, settings.DiscordPreferences),
            Telegram = MapBotConnection(channels.TelegramId, settings.TelegramPreferences)
        };

        return dto;
    }

    private static BotConnection? MapBotConnection(
        string? externalId,
        NotificationChannelPreference? prefs)
    {
        var connected = !string.IsNullOrEmpty(externalId);
        if (!connected && prefs == null) return null;

        return new BotConnection
        {
            Connected = connected,
            Enabled = prefs?.Enabled ?? false,
            EnabledCategories = prefs?.EnabledCategories ?? new()
        };
    }

    #region Bot Integration

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

    #endregion
}
