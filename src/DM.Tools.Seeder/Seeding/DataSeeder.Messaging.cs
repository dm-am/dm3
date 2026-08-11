using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Security;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;
using DM.Domain.Personal.Authorization;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Storage;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Infrastructure.Persistence.RelationalStorage;
using DbUserSettings = DM.Infrastructure.Persistence.Entities.Account.Settings.UserSettings;
using DM.Infrastructure.Persistence.Entities.Account.Settings;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Infrastructure.Persistence.Entities.Personal.Notepads;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Community;
using DM.Infrastructure.Persistence.Entities.Subscriptions;
using Microsoft.Extensions.Options;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchema;
using DbAttributeSpecification = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSpecification;
using DbStringConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.StringAttributeConstraints;
using DbBbCodeConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.BbCodeAttributeConstraints;
using DbListConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeConstraints;
using DbListValueKind = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListValueKind;
using DbListAttributeValue = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeValue;
using DbCharacterAttribute = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.CharacterAttribute;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbUsernameHistory = DM.Infrastructure.Persistence.Entities.Account.UsernameHistory;
using DbUserContact = DM.Infrastructure.Persistence.Entities.Account.UserContact;
using MongoDB.Driver;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// Bot channels connected on the stand, the way a real link connects them.
    /// </summary>
    /// <remarks>
    /// A channel counts as connected when two things are written together: the
    /// external id on the account and the delivery preferences in the settings
    /// document. BotLinkService reads the second one as the answer to "is this
    /// connected", so writing one without the other produces an account that
    /// shows a connected channel and refuses to configure it.
    ///
    /// Nothing is delivered anywhere by this: NotificationBotSender sends only
    /// when a bot token is configured, and the stand has none. What it buys is a
    /// settings page that can be opened, read and changed on a seeded site
    /// instead of one that answers 409 to every account.
    ///
    /// Two accounts and one channel each, so both states are on the stand at
    /// once: the primary account has Discord and no Telegram, the moderator has
    /// Telegram and no Discord.
    /// </remarks>
    private async Task ConnectBotChannels(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var connections = new[]
        {
            (Username: "SolohinLex", Channel: "discord", ExternalId: "310000000000000001"),
            (Username: "TestModerator", Channel: "telegram", ExternalId: "710000001"),
        };

        var settings = _mongoClient.GetCollection<DbUserSettings>();
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

            // The defaults a link writes: on, and the three categories a person
            // gets by default. Mirrored from BotLinkRepository rather than
            // invented, so a seeded account and a linked one read the same.
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

            var defaults = DbUserSettings.CreateDefault(user.UserId);
            var update = channel == "discord"
                ? Builders<DbUserSettings>.Update.Set(s => s.DiscordPreferences, preferences)
                : Builders<DbUserSettings>.Update.Set(s => s.TelegramPreferences, preferences);

            await settings.UpdateOneAsync(
                Builders<DbUserSettings>.Filter.Eq(s => s.UserId, user.UserId),
                Builders<DbUserSettings>.Update.Combine(
                    update,
                    Builders<DbUserSettings>.Update.SetOnInsert(s => s.Theme, defaults.Theme),
                    Builders<DbUserSettings>.Update.SetOnInsert(s => s.Paging, defaults.Paging)),
                new UpdateOptions { IsUpsert = true });

            connected++;
        }

        if (connected == 0)
        {
            result.Skipped++;
            return;
        }

        await _dbContext.SaveChangesAsync();
        result.Details.Add($"Connected {connected} bot channel(s)");
    }

    /// <summary>
    /// Private correspondence for the development accounts.
    /// </summary>
    /// <remarks>
    /// The seeded site had a messenger with nothing in it: only the global chat
    /// was written, so every account opened "Нет переписок" and the whole
    /// surface - the list, the previews, opening a conversation, editing a
    /// message - could only be looked at by writing to somebody first.
    ///
    /// Every chat gets messages and a last-message pointer, because a chat
    /// without one is not shown: the list reads only conversations somebody has
    /// written in. The pointer itself is set after the batch save, the way the
    /// game rooms do it, since Chat.LastMessageId and Message.ChatId reference
    /// each other and EF refuses to order the inserts otherwise.
    /// </remarks>
    private async Task CreateDirectChats(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var primary = users.FirstOrDefault(u => u.Username == "SolohinLex");
        if (primary == null)
        {
            return;
        }

        var existing = await _dbContext.Set<Chat>().AnyAsync(c => c.Type == ChatType.Direct);
        if (existing)
        {
            result.Skipped++;
            result.Details.Add("Direct chats already exist, skipping");
            return;
        }

        // Named accounts rather than a random draw: the e2e suite signs in as
        // the first two and asserts on what they can see.
        var companions = new[] { "TestUser", "TestModerator" }
            .Select(name => users.FirstOrDefault(u => u.Username == name))
            .Where(u => u != null)
            .Concat(users.Where(u => u.UserId != primary.UserId).Take(2))
            .Distinct()
            .Where(u => u!.UserId != primary.UserId)
            .Take(3)
            .ToList();

        var conversations = new[]
        {
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
        };

        var pending = new List<(Chat Chat, Guid LastMessageId)>();

        for (var index = 0; index < companions.Count; index++)
        {
            var companion = companions[index]!;
            // The readable address is taken from the sequence before the insert, the way the
            // repository takes it. Without it the seeded chat carries no PublicId, and the
            // address GET /v1/chats/{publicId} resolves by does not exist for it at all.
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
                    IsRemoved = false
                });
            }

            var script = conversations[index % conversations.Length];
            Guid lastMessageId = default;

            for (var line = 0; line < script.Length; line++)
            {
                var author = line % 2 == 0 ? primary : companion;
                var message = new Message
                {
                    MessageId = _guidFactory.Create(),
                    UserId = author.UserId,
                    ChatId = chat.ChatId,
                    CreatedUtc = now.AddHours(-(companions.Count - index) * 6 + line),
                    Text = script[line],
                    IsRemoved = false
                };

                _dbContext.Set<Message>().Add(message);
                lastMessageId = message.MessageId;
                result.MessagesCreated++;
            }

            pending.Add((chat, lastMessageId));
        }

        await _dbContext.SaveChangesAsync();

        foreach (var (chat, lastMessageId) in pending)
        {
            chat.LastMessageId = lastMessageId;
        }

        await _dbContext.SaveChangesAsync();

        result.Details.Add($"Created {pending.Count} direct chats for {primary.Username}");
    }

    private async Task CreateGlobalChatMessages(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var globalChatId = Chat.GlobalChatId;

        // Check if global chat exists
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

        // Check if messages already exist
        var existingMessagesCount = await _dbContext.Set<Message>().Where(m => m.ChatId == globalChatId).CountAsync();
        if (existingMessagesCount > 0)
        {
            result.Skipped++;
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

        Message? lastMessage = null;
        foreach (var text in messageTemplates)
        {
            var author = users[_random.Next(users.Count)];
            var message = new Message
            {
                MessageId = _guidFactory.Create(),
                UserId = author.UserId,
                ChatId = globalChatId,
                CreatedUtc = now.AddHours(-_random.Next(1, 168)),
                Text = text,
                IsRemoved = false
            };

            _dbContext.Set<Message>().Add(message);
            lastMessage = message;
            result.MessagesCreated++;
        }

        // Note: LastMessageId will be updated after SaveChangesAsync to avoid circular dependency

        result.Details.Add($"Created {result.MessagesCreated} global chat messages");
    }

    private async Task CreateGlobalChatEvents(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        if (users.Count == 0)
        {
            return;
        }

        // Events are created by a SeniorModerator (matches the Create intention)
        var organizer = users.FirstOrDefault(u => u.Role == UserRole.SeniorModerator) ?? users[0];
        // Invited participants for closed events (regular test accounts)
        var invitees = users.Where(u => u.Role == UserRole.RegularUser).Take(2).ToList();

        // Desired state after every reseed: ONE OPEN Live event running right
        // now (exercises the in-frame live banner without locking the chat
        // for non-participants) and TWO Scheduled events — one open, one
        // closed with invited participants (exercises the participants-only
        // message restriction and the "закрытый" mark in the upcoming list).
        // Descriptions are BBCode — rendered to HTML server-side on display.
        var eventTemplates = new (string Title, string Description, DateTimeOffset StartsUtc, TimeSpan? Duration, bool IsOpen, GlobalChatEventStatus Status)[]
        {
            (
                "Вечер быстрых зарисовок",
                "[b]Свободная импровизация — прямо сейчас в чате.[/b]\n" +
                "Как это устроено:\n" +
                "[ul][li]ведущий задает сцену одним сообщением[/li]" +
                "[li]каждый желающий добавляет реплику или действие своего персонажа[/li]" +
                "[li]каждые полчаса сцена меняется — успевайте вписаться[/li][/ul]\n" +
                "Присоединяйтесь в любой момент — вечер открыт для всех.",
                now.AddHours(-1), TimeSpan.FromHours(3), true, GlobalChatEventStatus.Live
            ),
            (
                "Турнир коротких историй",
                "[b]Соревнование рассказчиков: одна история — сто слов.[/b]\n" +
                "Правила турнира:\n" +
                "[ul][li]тема объявляется в момент старта[/li]" +
                "[li]на написание дается 30 минут[/li]" +
                "[li]победителя выбирают сами участники открытым голосованием[/li][/ul]\n" +
                "Победитель получает почетное упоминание в блоге модераторов.",
                now.AddDays(2), TimeSpan.FromHours(3), true, GlobalChatEventStatus.Scheduled
            ),
            (
                "Закрытый совет мастеров",
                "[b]Закрытая встреча ведущих игр.[/b]\n" +
                "Повестка:\n" +
                "[ul][li]обмен опытом по ведению долгих кампаний[/li]" +
                "[li]разбор сложных ситуаций с игроками[/li]" +
                "[li]планирование совместных межигровых событий[/li][/ul]\n" +
                "Писать в чате во время встречи могут только приглашенные участники.",
                now.AddDays(5), TimeSpan.FromMinutes(90), false, GlobalChatEventStatus.Scheduled
            ),
        };

        // Previous revisions of this step seeded a different event set —
        // remove those leftovers so a reseed converges on the state above
        // instead of piling up stale Scheduled rows (they were never Live,
        // so no messages reference them).
        var legacyTitles = new[] { "Литературный вечер", "Вечер вопросов и ответов" };
        var legacyEvents = await _dbContext.Set<GlobalChatEvent>()
            .Where(e => legacyTitles.Contains(e.Title))
            .ToListAsync();
        if (legacyEvents.Count > 0)
        {
            var legacyIds = legacyEvents.Select(e => e.GlobalChatEventId).ToList();
            var legacyParticipants = await _dbContext.Set<GlobalChatEventParticipant>()
                .Where(p => legacyIds.Contains(p.GlobalChatEventId))
                .ToListAsync();
            _dbContext.Set<GlobalChatEventParticipant>().RemoveRange(legacyParticipants);
            _dbContext.Set<GlobalChatEvent>().RemoveRange(legacyEvents);
            result.Details.Add($"Removed {legacyEvents.Count} legacy global chat events");
        }

        var templateTitles = eventTemplates.Select(t => t.Title).ToList();
        var existingEvents = await _dbContext.Set<GlobalChatEvent>()
            .Where(e => templateTitles.Contains(e.Title))
            .ToListAsync();

        // Only one event may be Live at a time (domain invariant, mirrored
        // from GlobalChatEventService.StartAsync) — if an unrelated event is
        // already running, do not seed a second Live one.
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

            // Idempotency by title, like the neighboring seed steps — but the
            // schedule fields are refreshed so a reseed always yields
            // "running now" / "upcoming" instead of dates frozen at the
            // previous run (participants are kept as-is).
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

            var chatEvent = new GlobalChatEvent
            {
                GlobalChatEventId = _guidFactory.Create(),
                Title = template.Title,
                Description = template.Description,
                StartsUtc = template.StartsUtc,
                Duration = template.Duration,
                IsOpen = template.IsOpen,
                Status = template.Status,
                // The Live event is seeded directly in the started state
                // (mirrors StartAsync: Status=Live + StartedUtc set) — it
                // "started" right at its scheduled time an hour ago.
                StartedUtc = template.Status == GlobalChatEventStatus.Live ? template.StartsUtc : null,
                CreatedByUserId = organizer.UserId,
                CreatedUtc = now,
            };
            _dbContext.Set<GlobalChatEvent>().Add(chatEvent);

            // Mirror the domain CreateAsync behavior: the creator becomes an
            // organizer participant
            _dbContext.Set<GlobalChatEventParticipant>().Add(new GlobalChatEventParticipant
            {
                GlobalChatEventParticipantId = _guidFactory.Create(),
                GlobalChatEventId = chatEvent.GlobalChatEventId,
                UserId = organizer.UserId,
                IsOrganizer = true,
                JoinedUtc = now,
            });

            // Closed events get invited participants so the participants-only
            // restriction can be exercised from test accounts
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
                        JoinedUtc = now,
                    });
                }
            }

            created++;
        }

        if (created == 0 && refreshed == 0)
        {
            result.Skipped++;
            result.Details.Add("Global chat events already in the desired state, skipping");
            return;
        }

        result.Details.Add($"Created {created} and refreshed {refreshed} global chat events");
    }
}
