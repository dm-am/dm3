using DM.Domain.Core.Chats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.RelationalStorage;
using DbUserSettings = DM.Infrastructure.Persistence.Entities.Account.Settings.UserSettings;
using DM.Infrastructure.Persistence.Entities.Account.Settings;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// Bot channels connected on the stand, the way a real link connects them.
    /// </summary>
    /// <remarks>
    /// The external id and the settings preferences are written together: BotLinkService reads the second.
    /// Nothing is delivered, the stand has no bot token. The primary account has Discord, the moderator Telegram.
    /// </remarks>
    private async Task ConnectBotChannels(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var connections = new[]
        {
            (Username: PrimaryLogin, Channel: "discord", ExternalId: "310000000000000001"),
            (Username: "TestModerator", Channel: "telegram", ExternalId: "710000001"),
        };

        var connected = 0;

        foreach (var (username, channel, externalId) in connections)
        {
            var user = users.FirstOrDefault(u => u.Username == username);
            if (user == null)
            {
                continue;
            }

            if (channel == "discord")
            {
                if (user.DiscordId != null) continue;
                user.DiscordId = externalId;
            }
            else
            {
                if (user.TelegramId != null) continue;
                user.TelegramId = externalId;
            }

            // The defaults a link writes, as BotLinkRepository does.
            var preferences = new NotificationChannelPreference
            {
                Enabled = true,
                EnabledCategories = new HashSet<NotificationCategory>
                {
                    NotificationCategory.Messages,
                    NotificationCategory.Games,
                    NotificationCategory.Security
                }
            };

            var settings = await _dbContext.UserSettings
                .FirstOrDefaultAsync(s => s.UserId == user.UserId);
            if (settings == null)
            {
                settings = DbUserSettings.CreateDefault(user.UserId);
                _dbContext.UserSettings.Add(settings);
            }

            if (channel == "discord")
            {
                settings.DiscordPreferences = preferences;
            }
            else
            {
                settings.TelegramPreferences = preferences;
            }

            connected++;
        }

        if (connected == 0)
        {
            return;
        }

        await _dbContext.SaveChangesAsync();
        result.Details.Add($"Connected {connected} bot channel(s)");
    }

    /// <summary>
    /// What the primary account and each companion write to each other, one
    /// script per conversation, taken in turn.
    /// </summary>
    private static readonly string[][] DirectChatScripts =
    [
        new[]
        {
            "Привет! Видел твою заявку в игру, беру.",
            "Спасибо! Когда стартуем?",
            "На выходных, я напишу в комнате.",
        },
        new[]
        {
            "Подскажи, где смотреть правила по броскам?",
            "В заметках игры, раздел про механику.",
        },
        new[]
        {
            "Отличный пост вышел, поздравляю с наградой.",
            "Спасибо, старался!",
        },
    ];

    /// <summary>
    /// Private correspondence for the development accounts.
    /// </summary>
    /// <remarks>
    /// A chat without a last-message pointer is not listed. The pointer is set after the save, as for the rooms.
    /// </remarks>
    private async Task CreateDirectChats(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var primary = users.FirstOrDefault(u => u.Username == PrimaryLogin);
        if (primary == null)
        {
            return;
        }

        var existing = await _dbContext.Set<Chat>().AnyAsync(c => c.Type == ChatType.Direct);
        if (existing)
        {
            result.Details.Add("Direct chats already exist, skipping");
            return;
        }

        // Named accounts: the e2e suite signs in as the first two.
        var companions = new[] { "TestUser", "TestModerator" }
            .Select(name => users.FirstOrDefault(u => u.Username == name))
            .OfType<DbUser>()
            .Concat(users.Where(u => u.UserId != primary.UserId).Take(2))
            .Distinct()
            .Where(u => u.UserId != primary.UserId)
            .Take(3)
            .ToList();

        var pending = new List<(Chat Chat, Guid LastMessageId)>();

        for (var index = 0; index < companions.Count; index++)
        {
            var companion = companions[index];
            pending.Add(await SeedDirectChat(
                primary,
                companion,
                DirectChatScripts[index % DirectChatScripts.Length],
                (companions.Count - index) * 6,
                result));
        }

        await SaveLinkingLastMessages(pending);

        result.Details.Add($"Created {pending.Count} direct chats for {primary.Username}");
    }

    /// <summary>
    /// Saves, then points each chat at its last message: EF refuses to order the Chat and Message cycle.
    /// </summary>
    private async Task SaveLinkingLastMessages(IEnumerable<(Chat Chat, Guid LastMessageId)> pending)
    {
        await _dbContext.SaveChangesAsync();

        foreach (var (chat, lastMessageId) in pending)
        {
            chat.LastMessageId = lastMessageId;
        }

        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// One conversation of the primary account with a companion: the chat, its
    /// two participants and the script, the primary account writing first.
    /// </summary>
    /// <returns>The chat and its last message, which is linked after the save.</returns>
    private async Task<(Chat Chat, Guid LastMessageId)> SeedDirectChat(
        DbUser primary, DbUser companion, string[] script, int hoursAgo,
        ComprehensiveSeedResult result)
    {
        // The readable address comes from the sequence before the insert: GET /v1/chats/{publicId} resolves by it.
        var chatSerialNumber = await SerialNumberAllocator.NextAsync<Chat>(_dbContext);
        var chat = new Chat
        {
            ChatId = _guidFactory.Create(),
            Type = ChatType.Direct,
            SerialNumber = chatSerialNumber,
            PublicId = _publicIdService.Encode(chatSerialNumber)
        };
        _dbContext.Set<Chat>().Add(chat);

        foreach (var participant in new[] { primary, companion })
        {
            _dbContext.Set<UserChatLink>().Add(new UserChatLink
            {
                UserChatLinkId = _guidFactory.Create(),
                UserId = participant.UserId,
                ChatId = chat.ChatId,
            });
        }

        Guid lastMessageId = default;

        for (var line = 0; line < script.Length; line++)
        {
            var author = line % 2 == 0 ? primary : companion;
            var message = new Message
            {
                MessageId = _guidFactory.Create(),
                UserId = author.UserId,
                ChatId = chat.ChatId,
                CreatedUtc = _now.AddHours(-hoursAgo + line),
                Text = script[line],
            };

            _dbContext.Set<Message>().Add(message);
            lastMessageId = message.MessageId;
            result.MessagesCreated++;
        }

        return (chat, lastMessageId);
    }

    private async Task CreateGlobalChatMessages(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var globalChatId = WellKnownChats.GlobalChatId;

        var globalChat = await _dbContext.Set<Chat>().FirstOrDefaultAsync(c => c.ChatId == globalChatId);
        if (globalChat == null)
        {
            globalChat = new Chat
            {
                ChatId = globalChatId,
                Type = ChatType.Global,
                Title = "Глобальный чат"
            };
            _dbContext.Set<Chat>().Add(globalChat);
        }

        var existingMessagesCount = await _dbContext.Set<Message>().Where(m => m.ChatId == globalChatId).CountAsync();
        if (existingMessagesCount > 0)
        {
            result.Details.Add($"Global chat messages already exist ({existingMessagesCount}), skipping");
            return;
        }

        var messageTemplates = new[]
        {
            "Привет всем! Как дела?",
            "Кто-нибудь играет сегодня?",
            "Ищу мастера для D&D one-shot",
            "Вчера была отличная сессия!",
            "Новички, не стесняйтесь спрашивать!",
            "Кто хочет присоединиться к нашей игре?",
            "Посоветуйте хорошую систему для новичков",
            "GURPS или Savage Worlds?",
            "Всем хорошего вечера!",
            "Спасибо за помощь!",
            "Когда следующий конкурс?",
            "Поздравляю победителей!",
        };

        foreach (var text in messageTemplates)
        {
            var author = users[_random.Next(users.Count)];
            var message = new Message
            {
                MessageId = _guidFactory.Create(),
                UserId = author.UserId,
                ChatId = globalChatId,
                CreatedUtc = _now.AddHours(-_random.Next(1, 168)),
                Text = text,
            };

            _dbContext.Set<Message>().Add(message);
            result.MessagesCreated++;
        }

        // LastMessageId is set after the save (UpdateGlobalChatLastMessage): EF refuses the foreign key cycle.
        result.Details.Add($"Created {result.MessagesCreated} global chat messages");
    }

    /// <summary>A global chat event the seed converges on, by its title.</summary>
    private sealed record GlobalChatEventTemplate(
        string Title, string Description, DateTimeOffset StartsUtc, TimeSpan? Duration, bool IsOpen,
        GlobalChatEventStatus Status);

    /// <summary>
    /// After every reseed: one open Live event running now and two Scheduled ones, one open and one
    /// closed with invited participants. Descriptions are BBCode.
    /// </summary>
    private static GlobalChatEventTemplate[] GlobalChatEventTemplates(DateTimeOffset now) =>
    [
        new(
            "Вечер быстрых зарисовок",
            "[b]Свободная импровизация — прямо сейчас в чате.[/b]\n" +
            "Как это устроено:\n" +
            "[ul][li]ведущий задает сцену одним сообщением[/li]" +
            "[li]каждый желающий добавляет реплику или действие своего персонажа[/li]" +
            "[li]каждые полчаса сцена меняется — успевайте вписаться[/li][/ul]\n" +
            "Присоединяйтесь в любой момент — вечер открыт для всех.",
            now.AddHours(-1), TimeSpan.FromHours(3), true, GlobalChatEventStatus.Live
        ),
        new(
            "Турнир коротких историй",
            "[b]Соревнование рассказчиков: одна история — сто слов.[/b]\n" +
            "Правила турнира:\n" +
            "[ul][li]тема объявляется в момент старта[/li]" +
            "[li]на написание дается 30 минут[/li]" +
            "[li]победителя выбирают сами участники открытым голосованием[/li][/ul]\n" +
            "Победитель получает почетное упоминание в блоге модераторов.",
            now.AddDays(2), TimeSpan.FromHours(3), true, GlobalChatEventStatus.Scheduled
        ),
        new(
            "Закрытый совет мастеров",
            "[b]Закрытая встреча ведущих игр.[/b]\n" +
            "Повестка:\n" +
            "[ul][li]обмен опытом по ведению долгих кампаний[/li]" +
            "[li]разбор сложных ситуаций с игроками[/li]" +
            "[li]планирование совместных межигровых событий[/li][/ul]\n" +
            "Писать в чате во время встречи могут только приглашенные участники.",
            now.AddDays(5), TimeSpan.FromMinutes(90), false, GlobalChatEventStatus.Scheduled
        ),
    ];

    private async Task CreateGlobalChatEvents(List<DbUser> users, ComprehensiveSeedResult result)
    {
        if (users.Count == 0)
        {
            return;
        }

        // Events are created by a SeniorModerator (matches the Create intention)
        var organizer = users.FirstOrDefault(u => u.Role == UserRole.SeniorModerator) ?? users[0];
        // Invited participants of closed events, the organizer excluded: one person twice on one list
        // violates the unique index.
        var invitees = users
            .Where(u => u.Role == UserRole.RegularUser && u.UserId != organizer.UserId)
            .Take(2)
            .ToList();

        var eventTemplates = GlobalChatEventTemplates(_now);

        var templateTitles = eventTemplates.Select(t => t.Title).ToList();
        var existingEvents = await _dbContext.Set<GlobalChatEvent>()
            .Where(e => templateTitles.Contains(e.Title))
            .ToListAsync();

        // One Live event at a time, as GlobalChatEventService.StartAsync keeps it.
        var hasForeignLiveEvent = await _dbContext.Set<GlobalChatEvent>()
            .AnyAsync(e => e.Status == GlobalChatEventStatus.Live && !templateTitles.Contains(e.Title));

        var created = 0;
        var refreshed = 0;
        foreach (var template in eventTemplates)
        {
            if (template.Status == GlobalChatEventStatus.Live && hasForeignLiveEvent)
            {
                result.Details.Add($"Skipped live event '{template.Title}': another event is already live");
                continue;
            }

            // Idempotent by title, but the schedule is refreshed, so a reseed is "running now" and "upcoming".
            var existing = existingEvents.FirstOrDefault(e => e.Title == template.Title);
            if (existing != null)
            {
                existing.StartsUtc = template.StartsUtc;
                existing.Duration = template.Duration;
                existing.IsOpen = template.IsOpen;
                existing.Status = template.Status;
                existing.StartedUtc = template.Status == GlobalChatEventStatus.Live ? template.StartsUtc : null;
                existing.EndedUtc = null;
                refreshed++;
                continue;
            }

            AddGlobalChatEvent(template, organizer, invitees);
            created++;
        }

        if (created == 0 && refreshed == 0)
        {
            result.Details.Add("Global chat events already in the desired state, skipping");
            return;
        }

        result.Details.Add($"Created {created} and refreshed {refreshed} global chat events");
    }


    /// <summary>
    /// One event as the domain creates it: the event, its organizer as a
    /// participant, and on a closed event the invited participants.
    /// </summary>
    private void AddGlobalChatEvent(
        GlobalChatEventTemplate template, DbUser organizer, IReadOnlyList<DbUser> invitees)
    {
        var chatEvent = new GlobalChatEvent
        {
            GlobalChatEventId = _guidFactory.Create(),
            Title = template.Title,
            Description = template.Description,
            StartsUtc = template.StartsUtc,
            Duration = template.Duration,
            IsOpen = template.IsOpen,
            Status = template.Status,
            // Seeded Live, as StartAsync leaves it, an hour after its scheduled start.
            StartedUtc = template.Status == GlobalChatEventStatus.Live ? template.StartsUtc : null,
            CreatedByUserId = organizer.UserId,
            CreatedUtc = _now,
        };
        _dbContext.Set<GlobalChatEvent>().Add(chatEvent);

        // As CreateAsync: the creator is an organizer participant
        _dbContext.Set<GlobalChatEventParticipant>().Add(new GlobalChatEventParticipant
        {
            GlobalChatEventParticipantId = _guidFactory.Create(),
            GlobalChatEventId = chatEvent.GlobalChatEventId,
            UserId = organizer.UserId,
            IsOrganizer = true,
            JoinedUtc = _now,
        });

        // For the participants-only restriction
        if (!template.IsOpen)
        {
            foreach (var invitee in invitees)
            {
                _dbContext.Set<GlobalChatEventParticipant>().Add(new GlobalChatEventParticipant
                {
                    GlobalChatEventParticipantId = _guidFactory.Create(),
                    GlobalChatEventId = chatEvent.GlobalChatEventId,
                    UserId = invitee.UserId,
                    IsOrganizer = false,
                    JoinedUtc = _now,
                });
            }
        }
    }
}
