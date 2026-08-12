using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Core.Configuration;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account.Settings;
using DM.Infrastructure.Persistence.MongoIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace DM.Workers.NotificationDispatcher.Implementation.Bot;

/// <inheritdoc />
internal class NotificationBotSender : MongoCollectionRepository<UserSettings>, INotificationBotSender
{
    private readonly DmDbContext _dbContext;
    private readonly BotConfiguration _botConfig;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SiteAddressConfiguration _siteAddresses;
    private readonly ILogger<NotificationBotSender> _logger;

    public NotificationBotSender(
        DmDbContext dbContext,
        DmMongoClient mongoClient,
        IOptions<BotConfiguration> botConfig,
        IHttpClientFactory httpClientFactory,
        IOptions<SiteAddressConfiguration> siteAddresses,
        ILogger<NotificationBotSender> logger) : base(mongoClient)
    {
        _dbContext = dbContext;
        _botConfig = botConfig.Value;
        _httpClientFactory = httpClientFactory;
        _siteAddresses = siteAddresses.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendIfEnabled(CreateNotification notification, EventType eventType, CancellationToken ct = default)
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

        // Get user settings from MongoDB
        var settingsList = await Collection
            .Find(Filter.In(s => s.UserId, userIds))
            .ToListAsync(ct);

        var settingsDict = settingsList.ToDictionary(s => s.UserId);

        // Get user bot IDs from PostgreSQL
        var userBotIds = await _dbContext.Users
            .Where(u => userIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.DiscordId, u.TelegramId })
            .ToDictionaryAsync(u => u.UserId, ct);

        // Two channels, two markup languages: Telegram parses the message as HTML and
        // Discord prints it as text. One message for both meant one of them was always
        // wrong.
        var discordMessage = BuildDiscordMessage(eventType, notification.Metadata, _siteAddresses);
        var telegramMessage = BuildTelegramMessage(eventType, notification.Metadata, _siteAddresses);

        foreach (var userId in userIds)
        {
            if (!userBotIds.TryGetValue(userId, out var botIds))
            {
                continue;
            }

            settingsDict.TryGetValue(userId, out var settings);

            // Send to Discord
            if (!string.IsNullOrEmpty(botIds.DiscordId) && !string.IsNullOrEmpty(_botConfig.DiscordBotToken))
            {
                if (ShouldSendToChannel(settings?.DiscordPreferences, category.Value))
                {
                    await SendDiscordMessage(botIds.DiscordId, discordMessage, ct);
                }
            }

            // Send to Telegram
            if (!string.IsNullOrEmpty(botIds.TelegramId) && !string.IsNullOrEmpty(_botConfig.TelegramBotToken))
            {
                if (ShouldSendToChannel(settings?.TelegramPreferences, category.Value))
                {
                    await SendTelegramMessage(botIds.TelegramId, telegramMessage, ct);
                }
            }
        }
    }

    private static bool ShouldSendToChannel(NotificationChannelPreference? prefs, NotificationCategory category)
    {
        if (prefs == null || !prefs.Enabled)
        {
            return false;
        }

        return prefs.EnabledCategories.Contains(category);
    }

    private async Task SendDiscordMessage(string userId, string message, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", _botConfig.DiscordBotToken);

            // Create DM channel
            var dmChannelResponse = await client.PostAsJsonAsync(
                "https://discord.com/api/v10/users/@me/channels",
                new { recipient_id = userId },
                ct);

            if (!dmChannelResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to create Discord DM channel for user {UserId}: {Status}",
                    userId, dmChannelResponse.StatusCode);
                return;
            }

            var dmChannel = await dmChannelResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            var channelId = dmChannel.GetProperty("id").GetString();

            // Send message
            var messageResponse = await client.PostAsJsonAsync(
                $"https://discord.com/api/v10/channels/{channelId}/messages",
                new { content = message },
                ct);

            if (!messageResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to send Discord message to user {UserId}: {Status}",
                    userId, messageResponse.StatusCode);
                return;
            }

            _logger.LogDebug("Sent Discord notification to user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send Discord notification to user {UserId}", userId);
        }
    }

    private async Task SendTelegramMessage(string chatId, string message, CancellationToken ct)
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
                return;
            }

            _logger.LogDebug("Sent Telegram notification to chat {ChatId}", chatId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send Telegram notification to chat {ChatId}", chatId);
        }
    }

    /// <summary>
    /// The Telegram message. It is sent with parse_mode HTML, so everything put into
    /// it is escaped first.
    /// </summary>
    /// <remarks>
    /// parse_mode is kept rather than dropped: bold is the only formatting the channel
    /// has, and Telegram accepts exactly what the shared encoder produces. Unescaped,
    /// a title holding an angle bracket made the whole message invalid markup,
    /// Telegram answered 400, and the notification was lost behind a warning in the
    /// log.
    ///
    /// Internal rather than private so that the escaping can be checked without a
    /// database, a broker and a bot token.
    /// </remarks>
    /// <param name="eventType">Event the message is about</param>
    /// <param name="metadata">Metadata bag of the notification</param>
    /// <param name="addresses">Addresses of the site, for the root of the link</param>
    internal static string BuildTelegramMessage(
        EventType eventType, object metadata, SiteAddressConfiguration addresses)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<b>Dungeon Master: {NotificationText.EscapeHtml(NotificationText.GetTitle(eventType))}</b>");
        sb.AppendLine();

        foreach (var (name, value) in NotificationText.ReadMetadata(metadata))
        {
            sb.AppendLine($"<b>{NotificationText.EscapeHtml(name)}:</b> {NotificationText.EscapeHtml(value)}");
        }

        // The destination the letter and the list lead to, in the markup this channel
        // reads. An anchor is the only way a Telegram message carries one.
        var target = NotificationLink.GetUrl(eventType, metadata, addresses);
        if (target != null)
        {
            sb.AppendLine();
            sb.AppendLine($"<a href=\"{NotificationText.EscapeHtml(target)}\">Перейти</a>");
        }

        return sb.ToString();
    }

    /// <summary>
    /// The Discord message. Discord renders the content as text, so it carries no
    /// markup and nothing in it is escaped.
    /// </summary>
    /// <remarks>
    /// One message used to be built for both channels in Telegram's HTML, and Discord
    /// printed it with the tags showing. Escaping that shared string would only have
    /// moved the damage: an ampersand in a game title would have reached Discord as an
    /// entity. The two channels do not share a markup language, so they no longer
    /// share a message.
    /// </remarks>
    /// <param name="eventType">Event the message is about</param>
    /// <param name="metadata">Metadata bag of the notification</param>
    /// <param name="addresses">Addresses of the site, for the root of the link</param>
    internal static string BuildDiscordMessage(
        EventType eventType, object metadata, SiteAddressConfiguration addresses)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Dungeon Master: {NotificationText.GetTitle(eventType)}");
        sb.AppendLine();

        foreach (var (name, value) in NotificationText.ReadMetadata(metadata))
        {
            sb.AppendLine($"{name}: {value}");
        }

        // The same destination the other two channels lead to, written as the bare
        // address: Discord links a URL of its own accord, and an anchor around it
        // would arrive with the tag showing.
        var target = NotificationLink.GetUrl(eventType, metadata, addresses);
        if (target != null)
        {
            sb.AppendLine();
            sb.AppendLine(target);
        }

        return sb.ToString();
    }
}
