using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using SystemUser = DM.Domain.Core.Identity.SystemUser;
using WarningPolicy = DM.Domain.Moderation.Features.Warnings.WarningPolicy;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Infrastructure.Persistence.Entities.Shared;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// The tracking token of the open guest ticket of the seed. Known, so that the
    /// guest page of a ticket can be opened on a stand and in the browser tests
    /// without reading the database: /support/track/5eedc0de5eedc0de5eedc0de5eedc0de.
    /// The form never issues it, its tokens come from a cryptographic generator.
    /// </summary>
    internal const string GuestTicketToken = "5eedc0de5eedc0de5eedc0de5eedc0de";

    /// <summary>
    /// The tracking token of the guest ticket the seed closes as spam.
    /// </summary>
    internal const string SpamTicketToken = "5eedc0de5eedc0de5eedc0dedeadbeef";

    /// <summary>
    /// A comment of a seeded conversation, by its number. Fixed rather than drawn
    /// from the factory: a draw here would move the identifier of every warning
    /// and ticket drawn after it.
    /// </summary>
    private static Guid SeedTicketCommentId(int number) =>
        Guid.Parse($"00000000-0000-0000-00d6-{number:D12}");

    /// <summary>
    /// Seeds a handful of tickets ("обращения") covering the role visibility
    /// matrix: user complaint and suggestion (Moderator scope), complaint
    /// about a junior moderator decision (SeniorModerator scope), guest
    /// support requests with GuestEmail and a Spam-status one (Admin scope).
    /// Idempotent: skips when any tickets already exist.
    /// </summary>
    /// <remarks>
    /// Every state a ticket is shown in has a row: waiting for moderation (the
    /// author answered last, or nobody answered yet), waiting for the user (a
    /// moderator asked last), closed after an answer in the conversation, and
    /// spam; the complaint closed with the answer that a warning was issued is
    /// written beside the warning (SeedWarnings). None carries a subject, because
    /// the forms do not ask for one and the screens name a ticket by its category.
    /// A complaint names its violator and its link in lines of its text, and a
    /// guest ticket the address the guest left, as the form writes them. Both
    /// guest tickets carry a tracking token, as every guest ticket does, the open
    /// one a known one.
    /// </remarks>
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
        var senior = users.FirstOrDefault(u => u.Role == UserRole.SeniorModerator) ?? moderator;
        if (regularUsers.Count < 2 || moderator == null || senior == null)
        {
            result.Details.Add("Tickets skipped: not enough users for reporter/target/moderator roles");
            return;
        }

        var reporter = regularUsers[0];
        var target = regularUsers[1];

        // Authenticated complaint about a user (Moderator scope): the moderator
        // asked for the comment the link did not point at, and waits for the author
        var complaint = new Ticket
        {
            TicketId = _guidFactory.Create(),
            UserId = reporter.UserId,
            TargetId = target.UserId,
            EntityType = "",
            Status = TicketStatus.WaitingForUser,
            Subtype = TicketSubtype.UserComplaint,
            CreatedUtc = now.AddDays(-2),
            Description = $"Пользователь {target.Username} оскорбляет участников в комментариях к игре. " +
                          $"Прошу принять меры.\n\nНарушитель: {target.Username}\n\n" +
                          "Ссылка на нарушение: /games/example",
            Comment = ""
        };

        // Authenticated suggestion, answered in the site markup and closed
        // (Moderator scope)
        var suggestion = new Ticket
        {
            TicketId = _guidFactory.Create(),
            UserId = target.UserId,
            EntityType = "",
            Status = TicketStatus.Closed,
            Subtype = TicketSubtype.SiteImprovementSuggestion,
            CreatedUtc = now.AddDays(-5),
            Description = "Предлагаю добавить сортировку списка игр по дате последнего поста — " +
                          "так проще находить живые игры.",
            Comment = "",
            ResolvedById = moderator.UserId,
            ResolvedUtc = now.AddDays(-4)
        };

        // Authenticated complaint about a junior moderator decision: a senior
        // moderator asked, the author answered, and it waits for moderation again
        // (SeniorModerator scope)
        var decisionComplaint = new Ticket
        {
            TicketId = _guidFactory.Create(),
            UserId = reporter.UserId,
            EntityType = "",
            Status = TicketStatus.WaitingForModeration,
            Subtype = TicketSubtype.ModeratorDecisionComplaint,
            CreatedUtc = now.AddDays(-1),
            Description = "Считаю, что предупреждение за флуд выдано несправедливо: сообщение " +
                          "было по теме обсуждения. Прошу пересмотреть решение.",
            Comment = ""
        };

        // Guest support request with a contact email, and a word the guest added
        // on his tracking page (Admin scope)
        var guestRequest = new Ticket
        {
            TicketId = _guidFactory.Create(),
            GuestEmail = "guest@example.com",
            TrackingToken = GuestTicketToken,
            EntityType = "",
            Status = TicketStatus.WaitingForModeration,
            Subtype = TicketSubtype.AccessRecovery,
            CreatedUtc = now.AddHours(-8),
            Description = "Не могу войти в аккаунт: письмо для восстановления пароля не приходит " +
                          "на почту. Аккаунт зарегистрирован давно, логин помню." +
                          "\n\nКонтакт для ответа: guest@example.com",
            Comment = ""
        };

        // Guest submission marked as spam (Admin scope, Spam status demo): closed
        // without a word, as spam is
        var spam = new Ticket
        {
            TicketId = _guidFactory.Create(),
            GuestEmail = "promo@spam.example.com",
            TrackingToken = SpamTicketToken,
            EntityType = "",
            Status = TicketStatus.Spam,
            Subtype = TicketSubtype.Bug,
            CreatedUtc = now.AddDays(-3),
            Description = "Лучшие цены на продвижение вашего сайта! Пишите нам прямо сейчас." +
                          "\n\nКонтакт для ответа: promo@spam.example.com",
            Comment = "",
            ResolvedById = moderator.UserId,
            ResolvedUtc = now.AddDays(-3).AddHours(2)
        };

        var tickets = new[] { complaint, suggestion, decisionComplaint, guestRequest, spam };
        var comments = new[]
        {
            SeedComment(1, complaint, moderator, now.AddDays(-2).AddHours(3),
                "Уточните, пожалуйста, в каком комментарии было оскорбление: ссылка ведет на игру целиком, " +
                "а комментариев к ней несколько сотен."),
            SeedComment(2, suggestion, moderator, now.AddDays(-4).AddMinutes(-5),
                "Спасибо за предложение! Записали его в [b]план развития[/b] сайта, " +
                "а когда до него дойдет очередь, о нем напишут в новостях проекта."),
            SeedComment(3, decisionComplaint, senior, now.AddDays(-1).AddHours(2),
                "Пришлите, пожалуйста, ссылку на сообщение, за которое выдано предупреждение: " +
                "в логе за этот день у вас их два."),
            SeedComment(4, decisionComplaint, reporter, now.AddDays(-1).AddHours(5),
                "Второе, в обсуждении правил: /forum/general. Я отвечал на вопрос новичка."),
            SeedComment(5, guestRequest, null, now.AddHours(-7),
                "Уточню: старая почта больше не работает, логин в аккаунте тот же, что в письме.")
        };

        _dbContext.Tickets.AddRange(tickets);
        _dbContext.TicketComments.AddRange(comments);
        await _dbContext.SaveChangesAsync();
        result.Details.Add(
            $"Tickets seeded: {tickets.Length} (complaint waiting for its author, answered suggestion, " +
            "moderator complaint answered by its author, guest recovery with a known token, spam) " +
            $"with {comments.Length} comments");
    }

    /// <summary>
    /// A comment of a seeded conversation; a null author is the guest who filed
    /// the ticket.
    /// </summary>
    private static TicketComment SeedComment(int number, Ticket ticket, DbUser? author, DateTimeOffset createdUtc,
        string text) =>
        new()
        {
            TicketCommentId = SeedTicketCommentId(number),
            TicketId = ticket.TicketId,
            AuthorId = author?.UserId,
            CreatedUtc = createdUtc,
            Text = text
        };

    /// <summary>
    /// Seeds five warnings over the readers and two moderators: three that stand
    /// as issued, one whose points a senior moderator took off entirely and one
    /// the moderator who issued it lowered himself. Idempotent: skips when any
    /// warnings already exist.
    /// </summary>
    /// <remarks>
    /// Nothing seeded a warning before, so the open log and the moderation list
    /// came up empty on a fresh stand and neither screen could be looked at. The
    /// two corrected rows are here for the same reason: the change of the points
    /// and the points that no longer match the ones the warning was issued with
    /// are only visible on a row that carries them.
    ///
    /// Four of the warnings name a real text of the seed, the way the button does
    /// it: a message of the global chat, a comment of a forum topic, of a game
    /// discussion and of a publication, each warned on its own author, an
    /// ordinary reader, with the text copied as the snapshot. The reasons are
    /// the ones those places have: an off-topic remark is an offence of the
    /// forum and not of a game discussion, which is about the game alone. The
    /// fifth is a warning on a profile (<see cref="OnProfile"/>), with the
    /// profile copied the way the button copies it.
    ///
    /// The comment under the publication carries the story a change of points
    /// usually has (the owner, 05.10: points come off for a mistake made in
    /// ignorance, an advert posted by accident): the invitation to another forum
    /// stood in the comment when the warning was issued, the author took it out
    /// the same day, and a senior moderator took the points off. The snapshot
    /// keeps the invitation, the comment no longer has it, and its edit history says when it went, which
    /// is what the "Отредактировано" mark of the log is read from.
    ///
    /// Where the seed holds no text of a kind, the warning falls back to naming
    /// nothing rather than inventing one.
    /// </remarks>
    private async Task SeedWarnings(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var existing = await _dbContext.Warnings.CountAsync();
        if (existing > 0)
        {
            result.Details.Add($"Warnings already seeded ({existing}), skipping");
            return;
        }

        var moderator = users.FirstOrDefault(u => u.Username == "TestModerator");
        var senior = users.FirstOrDefault(u => u.Username == "TestSeniorMod");
        var targets = users.Where(u => u.Role == UserRole.RegularUser).Take(3).ToList();
        if (moderator == null || senior == null || targets.Count < 3)
        {
            result.Details.Add("Warnings skipped: not enough users for target/moderator roles");
            return;
        }

        // The profile of the longest name, whose status and "О себе" the seed
        // writes (UpdateUserProfiles); any ordinary reader without it
        var avatarOffender = users.FirstOrDefault(u => u.Username == "LongestLoginPossible") ?? targets[1];
        var complainant = targets.First(u => u.UserId != avatarOffender.UserId);

        var chatMessage = await _dbContext.Messages
            .Where(m => m.ChatId == DbChat.GlobalChatId && !m.IsRemoved && m.Author.Role == UserRole.RegularUser)
            .OrderBy(m => m.CreatedUtc).ThenBy(m => m.MessageId)
            .Select(m => new Offending(m.MessageId, m.UserId, m.Text, WarningEntityType.Message, m.CreatedUtc))
            .FirstOrDefaultAsync();
        // Off the subject of its topic: the FAQ holds a remark about the look of
        // the site among its questions. Any comment of a topic will do without it.
        var topicComment = await FirstComment(
            c => _dbContext.Topics.Any(t => t.TopicId == c.EntityId), "Темная тема — это супер!");
        var gameComment = await FirstComment(c => _dbContext.Games.Any(g => g.GameId == c.EntityId));
        var publicationComment = await FirstComment(
            c => _dbContext.Publications.Any(p => p.PublicationId == c.EntityId));

        var advert = publicationComment == null
            ? null
            : publicationComment with
            {
                Text = publicationComment.Text + " Заходите к нам на форум, там это тоже разбирают"
            };
        var advertWarning = About(
            advert, targets[0], moderator, 2, "Реклама стороннего сайта", now.AddDays(-20), now);

        var warnings = new[]
        {
            About(chatMessage, targets[0], moderator, 2, "Флуд в общем чате", now.AddDays(-12), now),
            About(topicComment, targets[1], senior, 0, "Офтоп: сообщение не по теме топика", now.AddDays(-9), now),
            About(gameComment, targets[2], moderator, 1, "Флуд в обсуждении игры", now.AddDays(-6), now),
            // Taken off by a senior moderator: the advert was posted in ignorance
            // of the rules, and its author took it out the same day
            Corrected(advertWarning, senior, 0, "Позвал на свой форум по незнанию правил и сам убрал приглашение",
                now.AddDays(-18), now),
            // Lowered by the moderator who issued it, which he may do to his own;
            // issued on a complaint about the profile, so it names the profile
            Corrected(
                await OnProfile(avatarOffender, moderator, 3, "Неприемлемый аватар", now.AddDays(-4), now, result),
                moderator, 1, "Аватар убрал сам, оставил один балл", now.AddDays(-3), now)
        };

        _dbContext.Warnings.AddRange(warnings);

        if (advert != null)
        {
            // The edit that took the advert out: after the warning, by the author,
            // before the senior moderator looked at it
            _dbContext.CommentEdits.Add(new CommentEdit
            {
                CommentEditId = _guidFactory.Create(),
                CommentId = advert.EntityId,
                EditorUserId = advert.AuthorId,
                EditedUtc = NoLaterThan(advertWarning.CreatedUtc.AddHours(3), now)
            });
        }

        // The last of them, the avatar, was issued on a complaint of a reader
        var avatarComplaint = ComplaintAnsweredWith(warnings[^1], complainant, avatarOffender, moderator);
        _dbContext.Tickets.Add(avatarComplaint);
        _dbContext.TicketComments.Add(SeedComment(6, avatarComplaint, moderator, warnings[^1].CreatedUtc,
            "Спасибо, аватар действительно нарушает правила сайта. " +
            "Пользователю выдано предупреждение, картинку он должен заменить."));

        await _dbContext.SaveChangesAsync();
        result.Details.Add(
            $"Warnings seeded: {warnings.Length} (three as issued, one zeroed by a senior moderator, one on a profile " +
            "lowered by its author), one of them with the complaint it was issued on");
    }

    /// <summary>
    /// The complaint a warning was issued on, closed with the answer that says
    /// so, at the moment the warning was issued from the profile: the one closed
    /// complaint of the seed that ended in a sanction.
    /// </summary>
    /// <remarks>
    /// Written here and not with the other tickets, because its moments are the
    /// warning's and the warnings are written after the tickets. Created last in
    /// this step, so the identifiers drawn for the warnings stay what they were.
    /// </remarks>
    private Ticket ComplaintAnsweredWith(Warning warning, DbUser reporter, DbUser violator, DbUser moderator) =>
        new()
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

    /// <summary>
    /// A text of the seed a warning can name: the object, its author, its words,
    /// and when it was written.
    /// </summary>
    private sealed record Offending(
        Guid EntityId, Guid AuthorId, string Text, WarningEntityType EntityType, DateTimeOffset CreatedUtc);

    /// <summary>The earlier of a moment and now: the seed writes no future.</summary>
    private static DateTimeOffset NoLaterThan(DateTimeOffset moment, DateTimeOffset now) =>
        moment < now ? moment : now;

    /// <summary>
    /// The planned moment, or the floor when the plan falls before it, and never
    /// after now. The texts of the seed are dated relative to the moment it runs,
    /// as the warnings are, and a warning dated before the text it was given for
    /// read as a verdict on something not yet written.
    /// </summary>
    private static DateTimeOffset NoEarlierThan(DateTimeOffset planned, DateTimeOffset floor, DateTimeOffset now) =>
        NoLaterThan(planned < floor ? floor : planned, now);

    /// <summary>
    /// The first comment, in a fixed order, whose owner the predicate accepts and
    /// whose author is an ordinary reader: a moderator warns only below his own
    /// rank (ModerationRank), and the seed issues nothing the site would refuse.
    /// A comment with the preferred text, when one is given and found, comes
    /// first.
    /// </summary>
    private async Task<Offending?> FirstComment(
        Expression<Func<Comment, bool>> owner, string? preferredText = null) =>
        await _dbContext.Comments
            .Where(c => !c.IsRemoved && c.Author.Role == UserRole.RegularUser)
            .Where(owner)
            .OrderBy(c => preferredText != null && c.Text == preferredText ? 0 : 1)
            .ThenBy(c => c.CreatedUtc).ThenBy(c => c.CommentId)
            .Select(c => new Offending(c.CommentId, c.AuthorId, c.Text, WarningEntityType.Comment, c.CreatedUtc))
            .FirstOrDefaultAsync();

    /// <summary>
    /// A warning on a text of the seed, issued to its author with the text
    /// copied as the snapshot, as the button does it, an hour after the text at
    /// the earliest. Without such a text, a warning on the fallback reader that
    /// names nothing.
    /// </summary>
    private Warning About(
        Offending? offending, DbUser fallback, DbUser author, int points, string text, DateTimeOffset createdUtc,
        DateTimeOffset now)
    {
        if (offending == null)
        {
            return Warned(fallback, author, points, text, createdUtc);
        }

        var warning = Warned(fallback, author, points, text,
            NoEarlierThan(createdUtc, offending.CreatedUtc.AddHours(1), now));
        warning.TargetUserId = offending.AuthorId;
        warning.EntityId = offending.EntityId;
        warning.EntityType = offending.EntityType;
        warning.EntitySnapshot = offending.Text;
        return warning;
    }

    /// <summary>
    /// A warning on the profile of the reader, as the button issues it: the
    /// status and "О себе" copied, and the avatar pinned. The avatar is
    /// Assets/Seed/Chuck.png, which the reader took down six hours after the
    /// warning, so the upload is marked deleted with no author, as removing an
    /// avatar marks it.
    /// </summary>
    /// <remarks>
    /// The removal is days old, well past the grace period of the orphan sweeper,
    /// so the sweep the API runs at start is what proves the pin on a fresh
    /// stand: without it the picture is gone from the bucket by the first page
    /// load. The upload has a fixed identifier for the reason the bans of the ban
    /// list have (<see cref="BanListCaseIds"/>): drawn from the factory, it would
    /// shift the identifier of every warning and ticket written after it. When
    /// the image cannot be stored, the warning keeps the status and "О себе".
    /// </remarks>
    private async Task<Warning> OnProfile(
        DbUser target, DbUser author, int points, string text, DateTimeOffset createdUtc, DateTimeOffset now,
        ComprehensiveSeedResult result)
    {
        var warning = Warned(target, author, points, text, createdUtc);
        warning.EntityId = target.UserId;
        warning.EntityType = WarningEntityType.Profile;
        warning.EntitySnapshot = target.Info;
        warning.ProfileStatusSnapshot = target.Status;

        var upload = await _dbContext.Uploads.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.UploadId == FormerAvatarUploadId);
        if (upload == null)
        {
            try
            {
                upload = await SeedAvatarFromBytesAsync(
                    ReadEmbeddedSeedBytes("DM.Tools.Seeder.Assets.Seed.Chuck.png"),
                    declaredContentType: "image/png",
                    sourceFileName: "Chuck.png",
                    type: UploadType.UserAvatar,
                    uploadId: FormerAvatarUploadId,
                    userId: target.UserId,
                    entityId: target.UserId,
                    now: createdUtc.AddDays(-30));
            }
            catch (Exception ex)
            {
                result.Details.Add($"Former avatar of the warned profile not stored: {ex.Message}");
                return warning;
            }

            upload.IsRemoved = true;
            upload.DeletedByUserId = null;
            upload.DeletedUtc = NoLaterThan(createdUtc.AddHours(6), now);
            _dbContext.Uploads.Add(upload);
        }

        warning.ProfileAvatarUploadId = upload.UploadId;
        return warning;
    }

    /// <summary>
    /// The fixed identifier of the avatar the warning on a profile pins
    /// (<see cref="OnProfile"/>).
    /// </summary>
    private static readonly Guid FormerAvatarUploadId = new("00000000-0000-0000-00b1-000000000001");

    private Warning Warned(DbUser target, DbUser author, int points, string text, DateTimeOffset createdUtc) =>
        new()
        {
            WarningId = _guidFactory.Create(),
            TargetUserId = target.UserId,
            AuthorId = author.UserId,
            EntityId = Guid.Empty,
            EntityType = WarningEntityType.Unknown,
            CreatedUtc = createdUtc,
            Text = text,
            Points = points,
            OriginalPoints = points
        };

    /// <summary>
    /// The change of the points of a warning, a day after it at the earliest.
    /// </summary>
    private static Warning Corrected(
        Warning warning, DbUser changedBy, int points, string text, DateTimeOffset changedUtc, DateTimeOffset now)
    {
        warning.Points = points;
        warning.PointsChangedByUserId = changedBy.UserId;
        warning.PointsChangedUtc = NoEarlierThan(changedUtc, warning.CreatedUtc.AddDays(1), now);
        warning.PointsChangeText = text;
        return warning;
    }

    /// <summary>
    /// The bans of the seed: SolohinLex's chain, and beside it one ban of every
    /// kind the ban list draws (<see cref="SeedBanListCases"/>).
    /// </summary>
    private async Task SeedSolohinLexBans(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        await SeedSolohinLexChain(users, now, result);
        await SeedBanListCases(users, now, result);
    }

    /// <summary>
    /// Seed 10 bans for SolohinLex (target=SolohinLex, author=TestSeniorMod)
    /// — three tiers of the "резиновая уточка" chain are earned (BANS_1, BANS_3,
    /// BANS_10), the fourth (BANS_30) stays locked. The bans are past (Ended
    /// in the past) — historical records, not active restrictions. Issued by a
    /// senior moderator, because only that rank bans (BanService): signed by an
    /// ordinary moderator, the ban list showed a ban nobody of that rank could
    /// have issued. The chain reads as history from before SolohinLex was an
    /// administrator, whom no ban reaches now. Voluntary bans do not exist as a
    /// concept (owner decision),
    /// so <c>IsVoluntary</c> is always false in the seed. An eleventh ban, lifted
    /// early by a senior moderator with a reason, is added beside the ten for
    /// the ban list; the chain does not count it. Idempotent: a repeated seed
    /// does not create duplicates.
    /// </summary>
    private async Task SeedSolohinLexChain(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var solohin = users.FirstOrDefault(u => u.Username == "SolohinLex");
        var author = users.FirstOrDefault(u => u.Username == "TestSeniorMod");
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
        // Notes the way moderators of DM2 wrote them in its ban log, newest first:
        // a democratic ban is given by the sum of warning points and counted, and
        // the count goes on after the old bans burn out, each moderator writing
        // it in his own words.
        var reasons = new[]
        {
            "Десятый по сумме баллов (с учетом сгорания банов).",
            "По сумме баллов (9-й с учетом откатов)",
            "Восьмой по сумме баллов.",
            "Седьмой по сумме баллов (с учетом сгорания банов).",
            "6-й по сумме баллов",
            "Пятый по сумме баллов.",
            "По сумме баллов (4-й)",
            "Третий по сумме баллов.",
            "Второй по сумме баллов.",
            "Первый по сумме баллов.",
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
                AccessRestrictionPolicy = AccessPolicy.DemocraticBan,
                IsVoluntary = false,
            });
        }

        // One more, lifted early: the ban list draws a lift as a decision of its
        // own under the ban, with the name of whoever lifted it and the reason.
        // The reason is the one a lift usually has (the owner, 05.10): a mistake
        // made in ignorance of the rules, not a verdict found wrong.
        // An eleventh and not one of the ten, because a lifted ban is not a ban
        // served and the chain counts only those.
        _dbContext.Set<Ban>().Add(new Ban
        {
            BanId = _guidFactory.Create(),
            TargetUserId = solohin.UserId,
            AuthorId = author.UserId,
            StartedUtc = now.AddDays(-14),
            EndedUtc = now.AddDays(-7),
            Comment = "Реклама стороннего сайта в общем чате",
            AccessRestrictionPolicy = AccessPolicy.DemocraticBan,
            IsVoluntary = false,
            LiftedUtc = now.AddDays(-12),
            LiftedByUserId = author.UserId,
            LiftReason = "Ссылку дал по незнанию правил, сам ее удалил и извинился: бан снят досрочно",
        });
        await _dbContext.SaveChangesAsync();
        result.Details.Add($"SolohinLex bans seeded: {toCreate} created (total {targetCount}) for \"резиновая уточка\" demo, and one lifted early");
    }

    /// <summary>
    /// The bans the ban list needs beside SolohinLex's chain, one of each kind it
    /// draws: a permanent full ban from an administrator, a running ordinary one
    /// from a senior moderator, the site's automatic ban, filed under the system
    /// account, and a ban another moderator lifted early. Together with the chain
    /// that is three issuers and five banned readers on the first page of the list.
    /// </summary>
    /// <remarks>
    /// Their identifiers are fixed rather than drawn from the seeded factory:
    /// drawn here, four more identifiers would shift every identifier drawn after
    /// this step - the characters, the tickets, the warnings - and with them the
    /// addresses a stand built before this change had. Idempotent by the same
    /// identifiers.
    ///
    /// The permanent full ban takes the site away from LongestLoginPossible
    /// altogether, sign-in included (AccessPolicy.FullBan): the stand cannot be
    /// signed in to under that name.
    /// </remarks>
    private async Task SeedBanListCases(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        if (await _dbContext.Set<Ban>().AnyAsync(b => BanListCaseIds.Contains(b.BanId)))
        {
            result.Details.Add("Ban list cases already seeded, skipping");
            return;
        }

        var admin = users.FirstOrDefault(u => u.Username == "SolohinLex");
        var senior = users.FirstOrDefault(u => u.Username == "TestSeniorMod");
        var advertiser = users.FirstOrDefault(u => u.Username == "LongestLoginPossible");
        var offender = users.FirstOrDefault(u => u.Username == "Experienced");
        var warned = users.FirstOrDefault(u => u.Username == "Тест Елки");
        var forgiven = users.FirstOrDefault(u => u.Username == "RatingPositive");
        if (admin == null || senior == null || advertiser == null || offender == null || warned == null ||
            forgiven == null)
        {
            result.Details.Add("Ban list cases skipped: an issuer or a banned reader was not found");
            return;
        }

        var permanentStart = now.AddDays(-3);
        var automaticStart = now.AddDays(-10);
        _dbContext.Set<Ban>().AddRange(
            // Permanent: written as the window the issuing window writes, a
            // hundred years (BanService, Ban.PermanentYears)
            new Ban
            {
                BanId = BanListCaseIds[0],
                TargetUserId = advertiser.UserId,
                AuthorId = admin.UserId,
                StartedUtc = permanentStart,
                EndedUtc = permanentStart.AddYears(100),
                Comment = "Рекламный бот",
                AccessRestrictionPolicy = AccessPolicy.FullBan,
                IsVoluntary = false,
            },
            // Running now: the one ban of the list that offers "Снять бан"
            new Ban
            {
                BanId = BanListCaseIds[1],
                TargetUserId = offender.UserId,
                AuthorId = senior.UserId,
                StartedUtc = now.AddDays(-1),
                EndedUtc = now.AddDays(6),
                Comment = "Первый по сумме баллов (с учетом сгорания банов).",
                AccessRestrictionPolicy = AccessPolicy.DemocraticBan,
                IsVoluntary = false,
            },
            // The site's own, on warning points: what IssueAutomaticBan writes,
            // long over
            new Ban
            {
                BanId = BanListCaseIds[2],
                TargetUserId = warned.UserId,
                AuthorId = SystemUser.Id,
                StartedUtc = automaticStart,
                EndedUtc = automaticStart.AddHours(WarningPolicy.AutoBanDurationHours),
                Comment = WarningPolicy.AutoBanComment,
                AccessRestrictionPolicy = WarningPolicy.AutoBanPolicy,
                IsVoluntary = false,
            },
            // Lifted by somebody other than its issuer, two days into its week
            new Ban
            {
                BanId = BanListCaseIds[3],
                TargetUserId = forgiven.UserId,
                AuthorId = senior.UserId,
                StartedUtc = now.AddDays(-6),
                EndedUtc = now.AddDays(1),
                Comment = "Второй по сумме баллов.",
                AccessRestrictionPolicy = AccessPolicy.DemocraticBan,
                IsVoluntary = false,
                LiftedUtc = now.AddDays(-4),
                LiftedByUserId = admin.UserId,
                LiftReason = "Извинился, договорились",
            });
        await _dbContext.SaveChangesAsync();
        result.Details.Add("Ban list cases seeded: a permanent full ban, a running ban, an automatic ban and a lifted one");
    }

    /// <summary>
    /// The fixed identifiers of <see cref="SeedBanListCases"/>, in the order it
    /// writes them.
    /// </summary>
    private static readonly Guid[] BanListCaseIds =
    [
        new("00000000-0000-0000-00b0-000000000001"),
        new("00000000-0000-0000-00b0-000000000002"),
        new("00000000-0000-0000-00b0-000000000003"),
        new("00000000-0000-0000-00b0-000000000004"),
    ];
}
