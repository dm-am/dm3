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
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
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
            var author = users[Random.Shared.Next(users.Count)];
            var message = new Message
            {
                MessageId = _guidFactory.Create(),
                UserId = author.UserId,
                ChatId = globalChatId,
                CreatedUtc = now.AddHours(-Random.Shared.Next(1, 168)),
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
