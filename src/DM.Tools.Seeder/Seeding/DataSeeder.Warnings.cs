using DM.Domain.Core.Chats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Infrastructure.Persistence.Entities.Shared;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// Seeds five warnings over the readers and two moderators: three that stand
    /// as issued, one whose points a senior moderator took off entirely and one
    /// the moderator who issued it lowered himself. Idempotent: skips when any
    /// warnings already exist.
    /// </summary>
    /// <remarks>
    /// Four name a text of the seed as the button does, each warned on its author with the text as the snapshot;
    /// the fifth names a profile (<see cref="OnProfile"/>). The advert under the publication was taken out by its
    /// author the same day and its points taken off, so the snapshot and the edit history differ.
    /// Without a text of its kind, a warning names nothing.
    /// </remarks>
    private async Task SeedWarnings(List<DbUser> users, ComprehensiveSeedResult result)
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

        // The profile of the longest name, whose status and "О себе" the seed writes; any ordinary reader without it
        var avatarOffender = users.FirstOrDefault(u => u.Username == "LongestLoginPossible") ?? targets[1];
        var complainant = targets.First(u => u.UserId != avatarOffender.UserId);

        var chatMessage = await FirstChatMessage();
        // Off the subject: the FAQ holds a remark about the look of the site. Any topic comment without it.
        var topicComment = await FirstComment(
            c => _dbContext.Topics.Any(t => t.TopicId == c.EntityId), OffTopicFaqComment);
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
            advert, targets[0], moderator, 2, "Реклама стороннего сайта", _now.AddDays(-20));

        var warnings = new[]
        {
            About(chatMessage, targets[0], moderator, 2, "Флуд в глобальном чате", _now.AddDays(-12)),
            About(topicComment, targets[1], senior, 0, "Офтоп: комментарий не по теме топика", _now.AddDays(-9)),
            About(gameComment, targets[2], moderator, 1, "Флуд в обсуждении игры", _now.AddDays(-6)),
            // Taken off by a senior moderator: the advert was posted in ignorance
            // of the rules, and its author took it out the same day
            Corrected(advertWarning, senior, 0, "Позвал на свой форум по незнанию правил и сам убрал приглашение",
                _now.AddDays(-18), _now),
            // Lowered by the moderator who issued it, which he may do to his own;
            // issued on a complaint about the profile, so it names the profile
            Corrected(
                await OnProfile(avatarOffender, moderator, 3, "Неприемлемый аватар", _now.AddDays(-4)),
                moderator, 1, "Аватар убрал сам, оставил один балл", _now.AddDays(-3), _now)
        };

        _dbContext.Warnings.AddRange(warnings);

        if (advert != null)
        {
            // The author took the advert out after the warning and before the senior moderator looked at it
            _dbContext.CommentEdits.Add(new CommentEdit
            {
                CommentEditId = _guidFactory.Create(),
                CommentId = advert.EntityId,
                EditorUserId = advert.AuthorId,
                EditedUtc = NoLaterThan(advertWarning.CreatedUtc.AddHours(3), _now)
            });
        }

        // The last of them, the avatar, was issued on a complaint of a reader
        SeedComplaintAnsweredWith(warnings[^1], complainant, avatarOffender, moderator);

        await _dbContext.SaveChangesAsync();
        result.Details.Add(
            $"Warnings seeded: {warnings.Length} (three as issued, one zeroed by a senior moderator, one on a profile " +
            "lowered by its author), one of them with the complaint it was issued on");
    }

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
    /// The planned moment, or the floor when the plan falls before it, and never after now: a warning
    /// dated before its text reads as a verdict on something not yet written.
    /// </summary>
    private static DateTimeOffset NoEarlierThan(DateTimeOffset planned, DateTimeOffset floor, DateTimeOffset now) =>
        NoLaterThan(planned < floor ? floor : planned, now);

    /// <summary>
    /// The first message of the global chat, in a fixed order, written by an
    /// ordinary reader: a moderator warns only below his own rank.
    /// </summary>
    private async Task<Offending?> FirstChatMessage() =>
        await _dbContext.Messages
            .Where(m => m.ChatId == WellKnownChats.GlobalChatId && !m.IsRemoved && m.Author.Role == UserRole.RegularUser)
            .OrderBy(m => m.CreatedUtc).ThenBy(m => m.MessageId)
            .Select(m => new Offending(m.MessageId, m.UserId, m.Text, WarningEntityType.Message, m.CreatedUtc))
            .FirstOrDefaultAsync();

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
        Offending? offending, DbUser fallback, DbUser author, int points, string text, DateTimeOffset createdUtc)
    {
        if (offending == null)
        {
            return Warned(fallback, author, points, text, createdUtc);
        }

        var warning = Warned(fallback, author, points, text,
            NoEarlierThan(createdUtc, offending.CreatedUtc.AddHours(1), _now));
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
    /// The removal is past the grace period of the orphan sweeper, so the sweep at start proves the pin.
    /// A fixed identifier, for the reason of <see cref="BanListCaseIds"/>.
    /// </remarks>
    private async Task<Warning> OnProfile(
        DbUser target, DbUser author, int points, string text, DateTimeOffset createdUtc)
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
            upload = await SeedImageFromBytesAsync(
                ReadEmbeddedSeedBytes(ChuckImage),
                declaredContentType: "image/png",
                sourceFileName: "Chuck.png",
                type: UploadType.UserAvatar,
                uploadId: FormerAvatarUploadId,
                userId: target.UserId,
                entityId: target.UserId,
                uploadedUtc: createdUtc.AddDays(-30));

            upload.IsRemoved = true;
            upload.DeletedByUserId = null;
            upload.DeletedUtc = NoLaterThan(createdUtc.AddHours(6), _now);
            _dbContext.Uploads.Add(upload);
        }

        warning.ProfileAvatarUploadId = upload.UploadId;
        return warning;
    }

    /// <summary>
    /// The fixed identifier of the avatar the warning on a profile pins
    /// (<see cref="OnProfile"/>).
    /// </summary>
    private static readonly Guid FormerAvatarUploadId = FixedGuid(0xb1, 1);

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
}
