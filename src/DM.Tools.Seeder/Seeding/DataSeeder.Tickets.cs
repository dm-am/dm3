using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using TicketTextLimits = DM.Domain.Moderation.Features.Tickets.TicketTextLimits;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// The tracking token of the open guest ticket of the seed. Known, so that the
    /// guest page of a ticket can be opened on a stand and in the browser tests
    /// without reading the database: /support/track#token=5eedc0de5eedc0de5eedc0de5eedc0de.
    /// The form never issues it, its tokens come from a cryptographic generator.
    /// </summary>
    internal const string GuestTicketToken = "5eedc0de5eedc0de5eedc0de5eedc0de";

    internal const string SpamTicketToken = "5eedc0de5eedc0de5eedc0dedeadbeef";


    /// <summary>
    /// Seeds a handful of tickets ("обращения") covering the role visibility
    /// matrix: two user complaints, open and closed (Moderator scope), complaint
    /// about a junior moderator decision (SeniorModerator scope), guest
    /// support requests with GuestEmail and a Spam-status one (Admin scope),
    /// and TestUser's report of a bug whose conversation is full (Admin scope).
    /// Idempotent: skips when any tickets already exist.
    /// </summary>
    /// <remarks>
    /// Every state a ticket is shown in has a row. No subject, as the forms ask none; the complaint closed with
    /// a warning is written by SeedWarnings.
    /// </remarks>
    private async Task SeedTickets(List<DbUser> users, ComprehensiveSeedResult result)
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
        var senior = users.FirstOrDefault(u => u.Role == UserRole.SeniorModerator) ?? moderator;
        if (regularUsers.Count < 2 || moderator == null || senior == null)
        {
            result.Details.Add("Tickets skipped: not enough users for reporter/target/moderator roles");
            return;
        }

        var reporter = regularUsers[0];
        var target = regularUsers[1];

        var complaint = UserComplaint(reporter, target);
        var answered = AnsweredComplaint(target, reporter, moderator);
        var decisionComplaint = DecisionComplaint(reporter);
        var guestRequest = GuestRequest();
        var spam = GuestSpam(moderator);

        var tickets = new[] { complaint, answered, decisionComplaint, guestRequest, spam };
        var comments = new[]
        {
            SeedComment(1, complaint, moderator, _now.AddDays(-2).AddHours(3),
                "Уточните, пожалуйста, в каком комментарии было оскорбление: ссылка ведет на игру целиком, " +
                "а комментариев к ней несколько сотен."),
            SeedComment(2, answered, moderator, _now.AddDays(-4).AddMinutes(-5),
                "Сообщения [b]удалены[/b], пользователю напомнили правила чата. Обращение закрываем."),
            SeedComment(3, decisionComplaint, senior, _now.AddDays(-1).AddHours(2),
                "Пришлите, пожалуйста, ссылку на сообщение, за которое выдано предупреждение: " +
                "в логе за этот день у вас их два."),
            SeedComment(4, decisionComplaint, reporter, _now.AddDays(-1).AddHours(5),
                "Второе, в обсуждении правил: /forum/general. Я отвечал на вопрос новичка."),
            SeedComment(5, guestRequest, null, _now.AddHours(-7),
                "Уточню: старая почта больше не работает, логин в аккаунте тот же, что в письме.")
        };

        _dbContext.Tickets.AddRange(tickets);
        _dbContext.TicketComments.AddRange(comments);

        var member = users.FirstOrDefault(u => u.Username == "TestUser");
        var admin = users.FirstOrDefault(u => u.Role == UserRole.Admin);
        if (member != null && admin != null)
        {
            var (full, fullComments) = FullConversation(member.UserId, admin.UserId, _now);
            _dbContext.Tickets.Add(full);
            _dbContext.TicketComments.AddRange(fullComments);
        }

        await _dbContext.SaveChangesAsync();
        result.Details.Add(
            $"Tickets seeded: {tickets.Length} (complaint waiting for its author, answered complaint, " +
            "moderator complaint answered by its author, guest recovery with a known token, spam) " +
            $"with {comments.Length} comments, and TestUser's report of a bug with a full conversation");
    }

    internal static readonly Guid FullTicketId = FixedGuid(0xd5, 1);

    /// <remarks>
    /// Written on fixed identifiers, so nothing drawn after it moves. Filed weeks ago and in a category
    /// only administrators read, so the queues open with the tickets they opened with.
    /// </remarks>
    internal static (Ticket Ticket, IReadOnlyList<TicketComment> Comments) FullConversation(
        Guid memberId, Guid adminId, DateTimeOffset now)
    {
        var createdUtc = now.AddDays(-60);
        var comments = Enumerable.Range(1, TicketTextLimits.MaxComments)
            .Select(number => new TicketComment
            {
                // Past the six numbers of the short conversations.
                TicketCommentId = FixedGuid(0xd6, 0x100 + number),
                TicketId = FullTicketId,
                // The member first, the administrator last: the ticket waits for the member.
                AuthorId = number % 2 == 1 ? memberId : adminId,
                CreatedUtc = createdUtc.AddHours(12 * number),
                Text = number % 2 == 1
                    ? FullConversationMemberTexts[number / 2 % FullConversationMemberTexts.Length]
                    : FullConversationAdminTexts[(number / 2 - 1) % FullConversationAdminTexts.Length]
            })
            .ToList();

        var ticket = new Ticket
        {
            TicketId = FullTicketId,
            UserId = memberId,
            EntityType = "",
            Status = TicketStatus.WaitingForUser,
            Subtype = TicketSubtype.Bug,
            CreatedUtc = createdUtc,
            UpdatedUtc = comments[^1].CreatedUtc,
            Description = "Не загружается аватар персонажа: после выбора файла крутится индикатор, " +
                          "а через минуту форма пишет, что файл не принят. Картинка обычная, 300 КБ, JPEG.",
            Comment = ""
        };
        return (ticket, comments);
    }

    private static readonly string[] FullConversationMemberTexts =
    [
        "Попробовал еще раз, все так же: индикатор крутится, потом отказ.",
        "Другой браузер тоже не помог, картинку прикладываю ссылкой в профиле.",
        "Сейчас загрузилось, но после обновления страницы аватар снова пустой.",
        "Проверил на телефоне: та же история, файл не принят.",
    ];

    private static readonly string[] FullConversationAdminTexts =
    [
        "Спасибо, передали разработчикам. Пришлите, пожалуйста, название и версию браузера.",
        "Не воспроизводится у нас. Попробуйте, пожалуйста, файл меньшего размера.",
        "Выложили исправление. Проверьте еще раз и напишите, что получилось.",
        "Видим ошибку в журнале сервера, разбираемся. Напишем сюда, когда починим.",
    ];

    private Ticket UserComplaint(DbUser reporter, DbUser target) =>
        new()
        {
            TicketId = _guidFactory.Create(),
            UserId = reporter.UserId,
            TargetId = target.UserId,
            EntityType = "",
            Status = TicketStatus.WaitingForUser,
            Subtype = TicketSubtype.UserComplaint,
            CreatedUtc = _now.AddDays(-2),
            Description = $"Пользователь {target.Username} оскорбляет других пользователей в комментариях к игре. " +
                          $"Прошу принять меры.\n\nНарушитель: {target.Username}\n\n" +
                          "Ссылка на нарушение: /games/example",
            Comment = ""
        };

    private Ticket AnsweredComplaint(DbUser author, DbUser offender, DbUser moderator) =>
        new()
        {
            TicketId = _guidFactory.Create(),
            UserId = author.UserId,
            TargetId = offender.UserId,
            EntityType = "",
            Status = TicketStatus.Closed,
            Subtype = TicketSubtype.UserComplaint,
            CreatedUtc = _now.AddDays(-5),
            Description = $"Пользователь {offender.Username} третий день пишет в глобальном чате одно и то же " +
                          $"приглашение в свою игру.\n\nНарушитель: {offender.Username}",
            Comment = "",
            ResolvedById = moderator.UserId,
            ResolvedUtc = _now.AddDays(-4)
        };

    private Ticket DecisionComplaint(DbUser reporter) =>
        new()
        {
            TicketId = _guidFactory.Create(),
            UserId = reporter.UserId,
            EntityType = "",
            Status = TicketStatus.WaitingForModeration,
            Subtype = TicketSubtype.ModeratorDecisionComplaint,
            CreatedUtc = _now.AddDays(-1),
            Description = "Считаю, что предупреждение за флуд выдано несправедливо: сообщение " +
                          "было по теме обсуждения. Прошу пересмотреть решение.",
            Comment = ""
        };

    private Ticket GuestRequest() =>
        new()
        {
            TicketId = _guidFactory.Create(),
            GuestEmail = "guest@example.com",
            TrackingToken = GuestTicketToken,
            EntityType = "",
            Status = TicketStatus.WaitingForModeration,
            Subtype = TicketSubtype.AccessRecovery,
            CreatedUtc = _now.AddHours(-8),
            Description = "Не могу войти в аккаунт: письмо для восстановления пароля не приходит " +
                          "на почту. Аккаунт зарегистрирован давно, логин помню." +
                          "\n\nКонтакт для ответа: guest@example.com",
            Comment = ""
        };

    private Ticket GuestSpam(DbUser moderator) =>
        new()
        {
            TicketId = _guidFactory.Create(),
            GuestEmail = "promo@spam.example.com",
            TrackingToken = SpamTicketToken,
            EntityType = "",
            Status = TicketStatus.Spam,
            Subtype = TicketSubtype.Bug,
            CreatedUtc = _now.AddDays(-3),
            Description = "Лучшие цены на продвижение вашего сайта! Пишите нам прямо сейчас." +
                          "\n\nКонтакт для ответа: promo@spam.example.com",
            Comment = "",
            ResolvedById = moderator.UserId,
            ResolvedUtc = _now.AddDays(-3).AddHours(2)
        };

    // A comment of a seeded conversation; a null author is the guest who filed the ticket.
    private static TicketComment SeedComment(int number, Ticket ticket, DbUser? author, DateTimeOffset createdUtc,
        string text) =>
        new()
        {
            TicketCommentId = FixedGuid(0xd6, number),
            TicketId = ticket.TicketId,
            AuthorId = author?.UserId,
            CreatedUtc = createdUtc,
            Text = text
        };

    /// <remarks>
    /// Written by SeedWarnings with the warning's moments, last, so the identifiers of the warnings stay.
    /// </remarks>
    private void SeedComplaintAnsweredWith(Warning warning, DbUser reporter, DbUser violator, DbUser moderator)
    {
        var complaint = new Ticket
        {
            TicketId = _guidFactory.Create(),
            UserId = reporter.UserId,
            TargetId = violator.UserId,
            EntityType = "",
            Status = TicketStatus.Closed,
            Subtype = TicketSubtype.UserComplaint,
            CreatedUtc = warning.CreatedUtc.AddHours(-6),
            Description = $"У пользователя {violator.Username} на аватаре непристойная картинка, " +
                          "и ее видно под каждым его постом и комментарием.\n\n" +
                          $"Нарушитель: {violator.Username}\n\n" +
                          $"Ссылка на нарушение: /users/{violator.Username}",
            Comment = "",
            ResolvedById = moderator.UserId,
            ResolvedUtc = warning.CreatedUtc
        };
        _dbContext.Tickets.Add(complaint);
        _dbContext.TicketComments.Add(SeedComment(6, complaint, moderator, warning.CreatedUtc,
            "Спасибо, аватар действительно нарушает правила сайта. " +
            "Пользователю выдано предупреждение, картинку он должен заменить."));
    }
}
