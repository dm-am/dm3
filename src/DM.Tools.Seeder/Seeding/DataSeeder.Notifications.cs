using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Personal.Notifications;
using Microsoft.EntityFrameworkCore;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbNotification = DM.Infrastructure.Persistence.Entities.Personal.Notifications.Notification;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>The account whose list of notifications the seed fills.</summary>
    private const string NotifiedLogin = "TestUser";

    /// <summary>
    /// The spelling the store writes a payload in: the web defaults, so camelCase
    /// keys. A copy of NotificationRepository.MetadataOptions, which is private to
    /// the store; the seed writes the column itself, as it writes every other one.
    /// </summary>
    private static readonly JsonSerializerOptions NotificationMetadataOptions = new(JsonSerializerDefaults.Web)
    {
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// What the seeded notifications are about, read out of the content seeded
    /// above. A fact the fixture does not hold is null, and its row is not written.
    /// </summary>
    internal sealed record NotificationFacts(
        CommentFact? TopicComment,
        CharacterFact? AcceptedCharacter,
        GameFact? RecruitingGame,
        BlogFact? NewBlog,
        LikeFact? TopicCommentLike,
        GameFact? NewGame,
        LikeFact? GameCommentLike);

    /// <summary>A comment of somebody else in a topic the reader can open.</summary>
    internal sealed record CommentFact(Guid CommentId, Guid TopicId, string TopicTitle, string AuthorUsername);

    /// <summary>A character of the reader, accepted into a game somebody else masters.</summary>
    internal sealed record CharacterFact(
        Guid CharacterId, string Name, Guid GameId, string GameTitle, string MasterUsername);

    /// <summary>A game of somebody else the reader does not play in.</summary>
    internal sealed record GameFact(Guid GameId, string Title, string MasterUsername);

    /// <summary>A blog of somebody else.</summary>
    internal sealed record BlogFact(Guid BlogId, string Title, string AuthorUsername);

    /// <summary>
    /// A like on a comment of the reader: the topic or the game the comment stands
    /// in, and who liked it.
    /// </summary>
    internal sealed record LikeFact(Guid PlaceId, string PlaceTitle, string LikerUsername);

    /// <summary>
    /// One notification as it goes into the store: the event, the payload as the
    /// column holds it, when it arrived and whether the reader has read it.
    /// </summary>
    internal sealed record SeededNotification(EventType EventType, string Metadata, DateTimeOffset CreatedUtc, bool IsRead);

    /// <summary>
    /// Seeds the list of notifications of TestUser: four unread and three read
    /// when the fixture holds every fact they are about. Idempotent: skips when
    /// the reader already has notifications.
    /// </summary>
    /// <remarks>
    /// Nothing seeded a notification, so a fresh stand showed "Нет уведомлений"
    /// and the checks of the layout, which read the table of the list and the line
    /// of what is unread over it, had nothing to measure. The notifications on the
    /// owner's stand had gathered by themselves, which is why they passed there.
    ///
    /// Every event is one whose row fills both "Где" and "Кто": a place the title
    /// leads to and a person who did what happened. Each payload is the one the
    /// generator of the dispatcher writes for that event, field for field, and the
    /// identifiers in it are those of the content seeded above, so a title leads to
    /// a page that exists. SeededNotificationsShould in DM.Architecture.Tests puts
    /// every payload through the mapper of the API and holds it to the generator.
    ///
    /// Last of the steps: the rows draw identifiers, and drawn earlier they would
    /// move the identifier of everything drawn after them.
    /// </remarks>
    private async Task SeedNotifications(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var reader = users.FirstOrDefault(u => u.Username == NotifiedLogin);
        if (reader == null)
        {
            result.Details.Add($"Notifications skipped: no {NotifiedLogin}");
            return;
        }

        var existing = await _dbContext.NotificationRecipients.CountAsync(r => r.UserId == reader.UserId);
        if (existing > 0)
        {
            result.Skipped++;
            result.Details.Add($"Notifications of {NotifiedLogin} already exist ({existing}), skipping");
            return;
        }

        var notifications = NotificationsAbout(await ReadNotificationFacts(reader.UserId, now), now);
        foreach (var notification in notifications)
        {
            var notificationId = _guidFactory.Create();
            _dbContext.Notifications.Add(new DbNotification
            {
                NotificationId = notificationId,
                EventType = notification.EventType,
                CreatedUtc = notification.CreatedUtc,
                Metadata = notification.Metadata,
                Recipients =
                [
                    new NotificationRecipient
                    {
                        NotificationId = notificationId,
                        UserId = reader.UserId,
                        IsRead = notification.IsRead
                    }
                ]
            });
        }

        await _dbContext.SaveChangesAsync();
        result.NotificationsCreated = notifications.Count;
        result.Details.Add(
            $"Notifications seeded for {NotifiedLogin}: {notifications.Count}, " +
            $"{notifications.Count(n => !n.IsRead)} of them unread");
    }

    /// <summary>
    /// The notifications of the reader, newest first, the unread on top as they
    /// stand in a list nobody has opened for a day.
    /// </summary>
    /// <param name="facts">What the notifications are about</param>
    /// <param name="now">The instant the seed is laid out around</param>
    internal static IReadOnlyList<SeededNotification> NotificationsAbout(NotificationFacts facts, DateTimeOffset now) =>
        Payloads(facts)
            .Where(payload => payload.Metadata != null)
            .Select(payload => new SeededNotification(
                payload.EventType,
                JsonSerializer.Serialize(payload.Metadata, NotificationMetadataOptions),
                now - payload.Age,
                payload.IsRead))
            .ToList();

    /// <summary>
    /// The payload of every notification of the reader, null for a fact the
    /// fixture does not hold, with how long ago it arrived and whether it was read.
    /// </summary>
    /// <remarks>
    /// Written the way the generator named beside each writes it, member for member
    /// and with the same identifier encoding, because the mapper of the API finds the
    /// words, the place and the link by the names of those members.
    /// </remarks>
    private static IEnumerable<(EventType EventType, object? Metadata, TimeSpan Age, bool IsRead)> Payloads(
        NotificationFacts facts)
    {
        // NewCommentInSubscribedTopicNotificationGenerator
        yield return (EventType.NewCommentInSubscribedTopic, facts.TopicComment is { } comment
            ? new
            {
                CommentId = comment.CommentId,
                TopicId = comment.TopicId.EncodeToReadable(comment.TopicTitle),
                TopicTitle = comment.TopicTitle,
                AuthorUsername = comment.AuthorUsername
            }
            : null, TimeSpan.FromMinutes(35), false);

        // GameCharacterStatusChangedNotificationGenerator
        yield return (EventType.StatusCharacterAccepted, facts.AcceptedCharacter is { } character
            ? new
            {
                CharacterName = character.Name,
                CharacterId = character.CharacterId.EncodeToReadable(),
                GameTitle = character.GameTitle,
                GameId = character.GameId.EncodeToReadable(),
                NewStatus = CharacterStatus.Active.ToString(),
                MasterUsername = character.MasterUsername
            }
            : null, TimeSpan.FromHours(3), false);

        // GameRecruitmentOpenedNotificationGenerator
        yield return (EventType.GameRecruitmentOpened, facts.RecruitingGame is { } recruiting
            ? new
            {
                GameId = recruiting.GameId.EncodeToReadable(recruiting.Title),
                GameTitle = recruiting.Title,
                MasterUsername = recruiting.MasterUsername
            }
            : null, TimeSpan.FromHours(9), false);

        // NewBlogFromSubscribedAuthorNotificationGenerator
        yield return (EventType.NewBlogFromSubscribedAuthor, facts.NewBlog is { } blog
            ? new
            {
                BlogId = blog.BlogId.EncodeToReadable(blog.Title),
                BlogTitle = blog.Title,
                AuthorUsername = blog.AuthorUsername
            }
            : null, TimeSpan.FromHours(26), false);

        // TopicCommentLikedNotificationGenerator
        yield return (EventType.LikedTopicComment, facts.TopicCommentLike is { } topicLike
            ? new
            {
                LikerUsername = topicLike.LikerUsername,
                TopicId = topicLike.PlaceId.EncodeToReadable(topicLike.PlaceTitle),
                TopicTitle = topicLike.PlaceTitle
            }
            : null, TimeSpan.FromDays(2), true);

        // NewGameFromSubscribedAuthorNotificationGenerator
        yield return (EventType.NewGameFromSubscribedAuthor, facts.NewGame is { } game
            ? new
            {
                GameId = game.GameId.EncodeToReadable(game.Title),
                GameTitle = game.Title,
                AuthorUsername = game.MasterUsername
            }
            : null, TimeSpan.FromDays(4), true);

        // GameCommentLikedNotificationGenerator
        yield return (EventType.LikedGameComment, facts.GameCommentLike is { } gameLike
            ? new
            {
                LikerUsername = gameLike.LikerUsername,
                GameId = gameLike.PlaceId.EncodeToReadable(gameLike.PlaceTitle),
                GameTitle = gameLike.PlaceTitle
            }
            : null, TimeSpan.FromDays(6), true);
    }

    /// <summary>
    /// What the notifications of the reader are about, each picked from the
    /// content seeded above by an order that is an order: newest first, ties broken
    /// by identifier, nothing left to the database to decide.
    /// </summary>
    /// <remarks>
    /// Only what the reader can open: a topic of a board readers see, an active
    /// game or blog that passed premoderation. People are live accounts only: the
    /// filter of removed rows drops a removed author together with what joins him.
    /// Games the reader plays in are left out of the two about a game somebody
    /// else opened, since a player is not told about his own game that way.
    /// </remarks>
    private async Task<NotificationFacts> ReadNotificationFacts(Guid readerId, DateTimeOffset now)
    {
        var topicComment = await _dbContext.Comments
            .Where(c => c.AuthorId != readerId && c.CreatedUtc <= now)
            .Join(ReadableTopics(), c => c.EntityId, t => t.TopicId, (c, t) => new { Comment = c, Topic = t })
            .OrderByDescending(x => x.Comment.CreatedUtc)
            .ThenBy(x => x.Comment.CommentId)
            .Select(x => new CommentFact(
                x.Comment.CommentId, x.Topic.TopicId, x.Topic.Title, x.Comment.Author.Username))
            .FirstOrDefaultAsync();

        var character = await _dbContext.Characters
            .Where(c => c.AuthorId == readerId && !c.IsNpc && c.Status == CharacterStatus.Active &&
                        c.Game.Status == ModuleStatus.Active &&
                        c.Game.PremoderationStatus == PremoderationStatus.Approved &&
                        c.Game.MasterId != readerId)
            .OrderByDescending(c => c.CreatedUtc)
            .ThenBy(c => c.CharacterId)
            .Select(c => new CharacterFact(c.CharacterId, c.Name, c.GameId, c.Game.Title, c.Game.Master.Username))
            .FirstOrDefaultAsync();

        var recruiting = await NewestGame(OthersGames(readerId).Where(g => g.IsRecruitmentOpen));
        var recruitingId = recruiting?.GameId ?? Guid.Empty;
        var newGame = await NewestGame(OthersGames(readerId).Where(g => g.GameId != recruitingId));

        var blog = await _dbContext.Blogs
            .Where(b => b.AuthorId != readerId && b.Status == ModuleStatus.Active &&
                        b.PremoderationStatus == PremoderationStatus.Approved)
            .OrderByDescending(b => b.CreatedUtc)
            .ThenBy(b => b.BlogId)
            .Select(b => new BlogFact(b.BlogId, b.Title, b.Author.Username))
            .FirstOrDefaultAsync();

        var (topicLike, gameLike) = await ReadLikesOfReaderComments(readerId, now);

        return new NotificationFacts(topicComment, character, recruiting, blog, topicLike, newGame, gameLike);
    }

    /// <summary>
    /// The newest like on a comment of the reader in a topic, and the newest on one
    /// in the discussion of a game.
    /// </summary>
    private async Task<(LikeFact? Topic, LikeFact? Game)> ReadLikesOfReaderComments(Guid readerId, DateTimeOffset now)
    {
        var likes = _dbContext.Likes
            .Where(l => l.EntityType == LikeEntityType.Comment && l.UserId != readerId)
            .Join(_dbContext.Comments.Where(c => c.AuthorId == readerId && c.CreatedUtc <= now),
                l => l.EntityId, c => c.CommentId, (l, c) => new { Like = l, Comment = c });

        var topic = await likes
            .Join(ReadableTopics(), x => x.Comment.EntityId, t => t.TopicId,
                (x, t) => new { x.Like, x.Comment, PlaceId = t.TopicId, PlaceTitle = t.Title })
            .OrderByDescending(x => x.Comment.CreatedUtc)
            .ThenBy(x => x.Like.LikeId)
            .Select(x => new LikeFact(x.PlaceId, x.PlaceTitle, x.Like.User.Username))
            .FirstOrDefaultAsync();

        var game = await likes
            .Join(ReadableGames(), x => x.Comment.EntityId, g => g.GameId,
                (x, g) => new { x.Like, x.Comment, PlaceId = g.GameId, PlaceTitle = g.Title })
            .OrderByDescending(x => x.Comment.CreatedUtc)
            .ThenBy(x => x.Like.LikeId)
            .Select(x => new LikeFact(x.PlaceId, x.PlaceTitle, x.Like.User.Username))
            .FirstOrDefaultAsync();

        return (topic, game);
    }

    /// <summary>The newest of the given games, with the name of its master.</summary>
    private static Task<GameFact?> NewestGame(IQueryable<DbGame> games) => games
        .OrderByDescending(g => g.CreatedUtc)
        .ThenBy(g => g.GameId)
        .Select(g => new GameFact(g.GameId, g.Title, g.Master.Username))
        .FirstOrDefaultAsync();

    /// <summary>Topics of the boards an ordinary reader sees.</summary>
    private IQueryable<Topic> ReadableTopics() => _dbContext.Topics
        .Where(t => (t.Board.ViewPolicy & BoardAccessPolicy.RegularUser) != BoardAccessPolicy.None);

    /// <summary>Games open to every reader: active and past premoderation.</summary>
    private IQueryable<DbGame> ReadableGames() => _dbContext.Games
        .Where(g => g.Status == ModuleStatus.Active && g.PremoderationStatus == PremoderationStatus.Approved);

    /// <summary>Readable games somebody else masters and the reader has no character in.</summary>
    private IQueryable<DbGame> OthersGames(Guid readerId) => ReadableGames()
        .Where(g => g.MasterId != readerId &&
                    !_dbContext.Characters.Any(c => c.GameId == g.GameId && c.AuthorId == readerId));
}
