using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Core.Configuration;
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
    private readonly ILogger<NotificationBotSender> _logger;

    private static readonly Dictionary<EventType, string> EventTypeMessages = new()
    {
        // Games
        [EventType.StatusGameActive] = "???? ????????????",
        [EventType.StatusGameClosed] = "???? ???????",
        [EventType.StatusGameFrozen] = "???? ??????????",
        [EventType.StatusGameFinished] = "???? ?????????",
        [EventType.GameClosureWarning] = "?????????????? ? ???????? ????",
        [EventType.GameRecruitmentOpened] = "?????? ????? ? ????",
        [EventType.NewCharacter] = "????? ?????? ?? ?????????",
        [EventType.StatusCharacterAccepted] = "???????? ??????",
        [EventType.StatusCharacterDeclined] = "???????? ????????",
        [EventType.StatusCharacterExiled] = "???????? ??????",
        [EventType.StatusCharacterRetired] = "???????? ?????? ?? ????",
        [EventType.StatusCharacterDied] = "???????? ?????",
        [EventType.StatusCharacterResurrected] = "???????? ?????????",
        [EventType.StatusCharacterLeft] = "???????? ??????? ????",
        [EventType.StatusCharacterReturned] = "???????? ???????? ? ????",
        [EventType.AssignmentRequestCreated] = "??????????? ????? ???????????",
        [EventType.PlayerInvitationCreated] = "??????????? ? ????",
        [EventType.ReaderInvitationCreated] = "??????????? ????? ?????????",
        [EventType.RoomPendencyCreated] = "???????? ????",
        [EventType.RoomPendencyReminder] = "??????????? ? ????",
        [EventType.PostReviewed] = "???? ????????",

        // Forum
        [EventType.NewTopic] = "????? ???? ?? ??????",
        [EventType.LikedTopic] = "???? ?? ????",
        [EventType.NewTopicComment] = "????? ??????????? ? ????",
        [EventType.LikedTopicComment] = "???? ?? ???????????",

        // Blog
        [EventType.NewPublication] = "????? ??????????",
        [EventType.LikedPublication] = "???? ?? ??????????",
        [EventType.NewBlogComment] = "????? ??????????? ? ?????",
        [EventType.LikedBlogComment] = "???? ?? ??????????? ? ?????",
        [EventType.NewPublicationComment] = "????? ??????????? ? ??????????",
        [EventType.LikedPublicationComment] = "???? ?? ??????????? ? ??????????",

        // Messages
        [EventType.NewMessage] = "????? ?????????",
        [EventType.LikedMessage] = "???? ?? ?????????",

        // Subscriptions
        [EventType.NewCommentInSubscribedTopic] = "Новый комментарий в подписанной теме",
        [EventType.NewGameFromSubscribedAuthor] = "Новая игра от подписанного автора",
        [EventType.NewBlogFromSubscribedAuthor] = "Новый блог от подписанного автора",
        [EventType.NewTopicFromSubscribedAuthor] = "Новая тема от подписанного автора",
        [EventType.NewPostInSubscribedGame] = "Новый пост в подписанной игре",

        // Security
        [EventType.PasswordChanged] = "?????? ???????",
        [EventType.EmailChanged] = "Email ???????",
        [EventType.SuspiciousLoginActivity] = "?????????????? ?????????? ?????",

        // Moderation
        [EventType.WarningIssued] = "???????? ??????????????",
        [EventType.BanIssued] = "??????? ????????????",
        [EventType.BanLifted] = "?????????? ?????"
    };

    public NotificationBotSender(
        DmDbContext dbContext,
        DmMongoClient mongoClient,
        IOptions<BotConfiguration> botConfig,
        IHttpClientFactory httpClientFactory,
        ILogger<NotificationBotSender> logger) : base(mongoClient)
    {
        _dbContext = dbContext;
        _botConfig = botConfig.Value;
        _httpClientFactory = httpClientFactory;
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

        var message = BuildMessage(eventType, notification.Metadata);

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
                    await SendDiscordMessage(botIds.DiscordId, message, ct);
                }
            }

            // Send to Telegram
            if (!string.IsNullOrEmpty(botIds.TelegramId) && !string.IsNullOrEmpty(_botConfig.TelegramBotToken))
            {
                if (ShouldSendToChannel(settings?.TelegramPreferences, category.Value))
                {
                    await SendTelegramMessage(botIds.TelegramId, message, ct);
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

    private static string BuildMessage(EventType eventType, object metadata)
    {
        var title = EventTypeMessages.TryGetValue(eventType, out var msg) ? msg : "???????????";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>DM.AM: {title}</b>");
        sb.AppendLine();

        if (metadata != null)
        {
            try
            {
                var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
                {
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                using var doc = JsonDocument.Parse(metadataJson);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var name = FormatPropertyName(prop.Name);
                    var value = FormatPropertyValue(prop.Value);
                    if (!string.IsNullOrEmpty(value))
                    {
                        sb.AppendLine($"<b>{name}:</b> {value}");
                    }
                }
            }
            catch
            {
                // Ignore metadata parsing errors
            }
        }

        return sb.ToString();
    }

    private static string FormatPropertyName(string name) => name switch
    {
        "GameId" => "????",
        "GameTitle" => "???????? ????",
        "RoomId" => "???????",
        "RoomTitle" => "???????? ???????",
        "CharacterId" => "????????",
        "CharacterName" => "??? ?????????",
        "TopicId" => "????",
        "TopicTitle" => "???????? ????",
        "Username" => "????????????",
        "AuthorUsername" => "?????",
        "CreatedByUsername" => "??????",
        "BlogTitle" => "????",
        "PublicationTitle" => "??????????",
        "DaysPending" => "???? ????????",
        _ => name
    };

    private static string FormatPropertyValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? "",
        JsonValueKind.Number => element.ToString(),
        JsonValueKind.True => "??",
        JsonValueKind.False => "???",
        JsonValueKind.Null => "",
        _ => element.ToString()
    };
}
