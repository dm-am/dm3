using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Tracing;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Workers.NotificationDispatcher.Dispatching;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Workers.NotificationDispatcher.Bot;

/// <inheritdoc />
internal class NotificationBotSender : INotificationBotSender
{
    private readonly DmDbContext _dbContext;
    private readonly BotConfiguration _botConfig;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SiteAddressConfiguration _siteAddresses;
    private readonly ILogger<NotificationBotSender> _logger;

    public NotificationBotSender(
        DmDbContext dbContext,
        IOptions<BotConfiguration> botConfig,
        IHttpClientFactory httpClientFactory,
        IOptions<SiteAddressConfiguration> siteAddresses,
        ILogger<NotificationBotSender> logger)
    {
        _dbContext = dbContext;
        _botConfig = botConfig.Value;
        _httpClientFactory = httpClientFactory;
        _siteAddresses = siteAddresses.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendIfEnabled(CreateNotification notification, EventType eventType, CancellationToken ct)
    {
        var category = NotificationCategoryMapper.GetCategory(eventType);
        if (category == null)
        {
            return; // Event type not mapped to any category
        }

        var userIds = notification.UsersInterested.ToList();
        if (userIds.Count == 0)
        {
            return;
        }

        var settingsDict = await _dbContext.UserSettings
            .Where(s => userIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, ct);

        var userBotIds = await _dbContext.Users
            .Where(u => userIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.DiscordId, u.TelegramId })
            .ToDictionaryAsync(u => u.UserId, ct);

        // Telegram parses HTML and Discord prints text, so each gets its own message.
        var discordMessage = BuildDiscordMessage(eventType, notification.Metadata, _siteAddresses);
        var telegramMessage = BuildTelegramMessage(eventType, notification.Metadata, _siteAddresses);

        foreach (var userId in userIds)
        {
            if (!userBotIds.TryGetValue(userId, out var botIds))
            {
                continue;
            }

            settingsDict.TryGetValue(userId, out var settings);

            if (!string.IsNullOrEmpty(botIds.DiscordId) && !string.IsNullOrEmpty(_botConfig.DiscordBotToken) &&
                NotificationChannels.ShouldSend(settings?.DiscordPreferences, category.Value))
            {
                await Deliver(DiscordChannel, eventType,
                    () => SendDiscordMessage(botIds.DiscordId, discordMessage, ct));
            }

            if (!string.IsNullOrEmpty(botIds.TelegramId) && !string.IsNullOrEmpty(_botConfig.TelegramBotToken) &&
                NotificationChannels.ShouldSend(settings?.TelegramPreferences, category.Value))
            {
                await Deliver(TelegramChannel, eventType,
                    () => SendTelegramMessage(botIds.TelegramId, telegramMessage, ct));
            }
        }
    }

    /// <summary>Channel name the two bots are counted under.</summary>
    private const string DiscordChannel = "discord";

    /// <inheritdoc cref="DiscordChannel" />
    private const string TelegramChannel = "telegram";

    /// <summary>
    /// Runs one send and counts what the channel refused.
    /// </summary>
    /// <remarks>
    /// The message is already consumed, so this counter is the only trace of a channel that is down.
    /// </remarks>
    private static async Task Deliver(string channel, EventType eventType, Func<Task<bool>> send)
    {
        if (!await send())
        {
            MessagingMetrics.DeliveryFailed.Add(1,
                MessagingMetrics.Channel(channel),
                new KeyValuePair<string, object?>("event", eventType.ToString()));
        }
    }

    private async Task<bool> SendDiscordMessage(string userId, string message, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", _botConfig.DiscordBotToken);

            var dmChannelResponse = await client.PostAsJsonAsync(
                "https://discord.com/api/v10/users/@me/channels",
                new { recipient_id = userId },
                ct);

            if (!dmChannelResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to create Discord DM channel for user {UserId}: {Status}",
                    userId, dmChannelResponse.StatusCode);
                return false;
            }

            var dmChannel = await dmChannelResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            var channelId = dmChannel.GetProperty("id").GetString();

            var messageResponse = await client.PostAsJsonAsync(
                $"https://discord.com/api/v10/channels/{channelId}/messages",
                new { content = message },
                ct);

            if (!messageResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to send Discord message to user {UserId}: {Status}",
                    userId, messageResponse.StatusCode);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send Discord notification to user {UserId}", userId);
            return false;
        }
    }

    private async Task<bool> SendTelegramMessage(string chatId, string message, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsJsonAsync(
                $"https://api.telegram.org/bot{_botConfig.TelegramBotToken}/sendMessage",
                new
                {
                    chat_id = chatId,
                    text = message,
                    parse_mode = "HTML"
                },
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Failed to send Telegram message to chat {ChatId}: {Status} - {Error}",
                    chatId, response.StatusCode, error);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send Telegram notification to chat {ChatId}", chatId);
            return false;
        }
    }

    /// <summary>
    /// The Telegram message. It is sent with parse_mode HTML, so everything put into
    /// it is escaped first.
    /// </summary>
    /// <remarks>
    /// Internal so that the escaping can be checked without a database and a bot token.
    /// </remarks>
    /// <param name="eventType">Event the message is about</param>
    /// <param name="metadata">Metadata bag of the notification</param>
    /// <param name="addresses">Addresses of the site, for the root of the link</param>
    internal static string BuildTelegramMessage(
        EventType eventType, object metadata, SiteAddressConfiguration addresses)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<b>{NotificationText.TitlePrefix}{NotificationText.EscapeHtml(NotificationWording.GetTitle(eventType))}</b>");
        sb.AppendLine();

        foreach (var (name, value) in NotificationWording.ReadMetadata(metadata))
        {
            sb.AppendLine($"<b>{NotificationText.EscapeHtml(name)}:</b> {NotificationText.EscapeHtml(value)}");
        }

        var target = NotificationLink.GetUrl(eventType, metadata, addresses);
        if (target != null)
        {
            sb.AppendLine();
            sb.AppendLine($"<a href=\"{NotificationText.EscapeHtml(target)}\">{NotificationLink.Caption}</a>");
        }

        return sb.ToString();
    }

    /// <summary>
    /// The Discord message. Discord renders the content as text, so it carries no
    /// markup and nothing in it is escaped.
    /// </summary>
    /// <param name="eventType">Event the message is about</param>
    /// <param name="metadata">Metadata bag of the notification</param>
    /// <param name="addresses">Addresses of the site, for the root of the link</param>
    internal static string BuildDiscordMessage(
        EventType eventType, object metadata, SiteAddressConfiguration addresses)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{NotificationText.TitlePrefix}{NotificationWording.GetTitle(eventType)}");
        sb.AppendLine();

        foreach (var (name, value) in NotificationWording.ReadMetadata(metadata))
        {
            sb.AppendLine($"{name}: {value}");
        }

        // The bare address: Discord links a URL by itself and would show an anchor tag.
        var target = NotificationLink.GetUrl(eventType, metadata, addresses);
        if (target != null)
        {
            sb.AppendLine();
            sb.AppendLine(target);
        }

        return sb.ToString();
    }
}
