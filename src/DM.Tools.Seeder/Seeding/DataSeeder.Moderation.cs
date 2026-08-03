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
    /// <summary>
    /// Seeds a handful of tickets ("обращения") covering the role visibility
    /// matrix: user complaint and suggestion (Moderator scope), complaint
    /// about a junior moderator decision (SeniorModerator scope), guest
    /// support requests with GuestEmail and a Spam-status one (Admin scope).
    /// Idempotent: skips when any tickets already exist.
    /// </summary>
    private async Task SeedTickets(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var existing = await _dbContext.Tickets.CountAsync();
        if (existing > 0)
        {
            result.Details.Add($"Tickets already seeded ({existing}), skipping");
            return;
        }

        var regularUsers = users.Where(u => u.Role == UserRole.RegularUser).ToList();
        var moderator = users.FirstOrDefault(u => u.Role == UserRole.Moderator)
                        ?? users.FirstOrDefault(u => u.Role == UserRole.SeniorModerator);
        if (regularUsers.Count < 2 || moderator == null)
        {
            result.Details.Add("Tickets skipped: not enough users for reporter/target/moderator roles");
            return;
        }

        var reporter = regularUsers[0];
        var target = regularUsers[1];

        var tickets = new[]
        {
            // Authenticated complaint about a user (Moderator scope)
            new Ticket
            {
                TicketId = _guidFactory.Create(),
                UserId = reporter.UserId,
                TargetId = target.UserId,
                EntityType = "",
                Status = TicketStatus.WaitingForModeration,
                Subtype = TicketSubtype.UserComplaint,
                CreatedUtc = now.AddDays(-2),
                Description = $"Пользователь {target.Username} оскорбляет участников в комментариях к игре. " +
                              "Прошу принять меры.\n\nСсылка на нарушение: https://dm.am/games/example",
                Comment = "Жалоба на поведение в комментариях"
            },
            // Authenticated suggestion, answered by a moderator (Moderator scope)
            new Ticket
            {
                TicketId = _guidFactory.Create(),
                UserId = target.UserId,
                EntityType = "",
                Status = TicketStatus.WaitingForUser,
                Subtype = TicketSubtype.SiteImprovementSuggestion,
                CreatedUtc = now.AddDays(-5),
                Description = "Предлагаю добавить сортировку списка игр по дате последнего поста — " +
                              "так проще находить живые игры.",
                Comment = "Предложение по списку игр",
                AssignedModeratorId = moderator.UserId,
                AnswerAuthorId = moderator.UserId,
                Answer = "Спасибо за предложение! Передали разработчикам, уточните, пожалуйста, " +
                         "какой порядок сортировки вы ожидаете по умолчанию."
            },
            // Authenticated complaint about a junior moderator decision (SeniorModerator scope)
            new Ticket
            {
                TicketId = _guidFactory.Create(),
                UserId = reporter.UserId,
                EntityType = "",
                Status = TicketStatus.WaitingForModeration,
                Subtype = TicketSubtype.ModeratorDecisionComplaint,
                CreatedUtc = now.AddDays(-1),
                Description = "Считаю, что предупреждение за флуд выдано несправедливо: сообщение " +
                              "было по теме обсуждения. Прошу пересмотреть решение.",
                Comment = "Несогласие с предупреждением"
            },
            // Guest support request with a contact email (Admin scope)
            new Ticket
            {
                TicketId = _guidFactory.Create(),
                GuestEmail = "guest@example.com",
                EntityType = "",
                Status = TicketStatus.WaitingForModeration,
                Subtype = TicketSubtype.AccessRecovery,
                CreatedUtc = now.AddHours(-8),
                Description = "Не могу войти в аккаунт: письмо для восстановления пароля не приходит " +
                              "на почту. Аккаунт зарегистрирован давно, логин помню.",
                Comment = "Не приходит письмо восстановления"
            },
            // Guest submission marked as spam (Admin scope, Spam status demo)
            new Ticket
            {
                TicketId = _guidFactory.Create(),
                GuestEmail = "promo@spam.example.com",
                EntityType = "",
                Status = TicketStatus.Spam,
                Subtype = TicketSubtype.Bug,
                CreatedUtc = now.AddDays(-3),
                Description = "Лучшие цены на продвижение вашего сайта! Пишите нам прямо сейчас.",
                Comment = "Реклама",
                AssignedModeratorId = moderator.UserId,
                ResolvedUtc = now.AddDays(-3).AddHours(2)
            }
        };

        _dbContext.Tickets.AddRange(tickets);
        await _dbContext.SaveChangesAsync();
        result.Details.Add($"Tickets seeded: {tickets.Length} (complaint, suggestion, moderator complaint, guest recovery, spam)");
    }

    /// <summary>
    /// Seed 10 bans for SolohinLex (target=SolohinLex, author=TestModerator)
    /// — three tiers of the "резиновая уточка" chain are earned (BANS_1, BANS_3,
    /// BANS_10), the fourth (BANS_30) stays locked. The bans are past (Ended
    /// in the past), AccessRestrictionPolicy is not set — historical records,
    /// not active restrictions. Regular moderator bans: voluntary bans
    /// do not exist as a concept (owner decision), so
    /// <c>IsVoluntary</c> is always false in the seed. Idempotent: a repeated
    /// seed does not create duplicates.
    /// </summary>
    private async Task SeedSolohinLexBans(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var solohin = users.FirstOrDefault(u => u.Username == "SolohinLex");
        var author = users.FirstOrDefault(u => u.Username == "TestModerator")
                   ?? users.FirstOrDefault(u => u.Username == "TestSeniorMod");
        if (solohin == null || author == null)
        {
            result.Details.Add("SolohinLex bans skipped: target or author user not found");
            return;
        }

        var existing = await _dbContext.Set<Ban>()
            .Where(b => b.TargetUserId == solohin.UserId)
            .CountAsync();
        if (existing >= 10)
        {
            result.Details.Add($"SolohinLex already has {existing} bans, skipping ban seed");
            return;
        }

        const int targetCount = 10;
        var toCreate = targetCount - existing;
        var reasons = new[]
        {
            "Спам в глобальном чате",
            "Оскорбление участников",
            "Нарушение правил конкурса",
            "Флуд в форумной теме",
            "Подозрительная активность",
            "Эксплуатация багов",
            "Нарушение правил игры",
            "Многократные жалобы",
            "Нарушение этикета",
            "Тестовый бан (демо)",
        };
        for (var i = 0; i < toCreate; i++)
        {
            var ago = (existing + i + 1) * 30; // spread evenly over recent months
            _dbContext.Set<Ban>().Add(new Ban
            {
                BanId = _guidFactory.Create(),
                TargetUserId = solohin.UserId,
                AuthorId = author.UserId,
                StartedUtc = now.AddDays(-ago),
                EndedUtc = now.AddDays(-ago + 7),
                Comment = reasons[(existing + i) % reasons.Length],
                AccessRestrictionPolicy = AccessPolicy.NotSpecified,
                IsVoluntary = false,
                IsRemoved = false,
            });
        }
        await _dbContext.SaveChangesAsync();
        result.Details.Add($"SolohinLex bans seeded: {toCreate} created (total {targetCount}) for \"резиновая уточка\" demo");
    }
}
