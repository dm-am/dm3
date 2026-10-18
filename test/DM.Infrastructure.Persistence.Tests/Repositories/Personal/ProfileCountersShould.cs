using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Repositories.Personal;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbBan = DM.Infrastructure.Persistence.Entities.Moderation.Ban;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbBlogAssistant = DM.Infrastructure.Persistence.Entities.Blog.BlogAssistant;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbEndorsement = DM.Infrastructure.Persistence.Entities.Community.UserEndorsement;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAssistant = DM.Infrastructure.Persistence.Entities.Game.Links.GameAssistant;
using DbGameReview = DM.Infrastructure.Persistence.Entities.Game.GameReview;
using DbLike = DM.Infrastructure.Persistence.Entities.Shared.Like;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;
using DbSubscription = DM.Infrastructure.Persistence.Entities.Subscriptions.Subscription;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbUsernameHistory = DM.Infrastructure.Persistence.Entities.Account.UsernameHistory;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Personal;

/// <summary>
/// Every number on a profile is counted from its own rows.
/// </summary>
/// <remarks>
/// The profile is filled by one routine of some twenty aggregates, each written
/// as a GROUP BY into a dictionary and then copied onto the user by name. A
/// refactoring that reads one dictionary into the wrong member, or drops a
/// filter from one aggregate, changes one number on one page, and only a check
/// of every number at once notices.
///
/// One user carries something of every kind, every counter at a value no other
/// counter has, and a removed or otherwise excluded row beside each kind: a
/// counter read from its neighbour's dictionary, or one that stopped leaving a
/// row out, comes back as a different number. The counters are also walked by
/// type, so a counter added to the user without a row seeded here fails as a
/// zero rather than passing unseen.
///
/// In memory, as the list half of the same routine is (UserListEnrichmentShould):
/// what is held is which rows each aggregate counts and where its answer goes,
/// and the profile endpoint runs the same statements against Postgres.
/// </remarks>
public class ProfileCountersShould
{
    private static readonly Guid SubjectId = Guid.Parse("c0de0000-0000-4000-8000-000000000001");
    private static readonly Guid HostId = Guid.Parse("c0de0000-0000-4000-8000-000000000002");
    private static readonly Guid PeerId = Guid.Parse("c0de0000-0000-4000-8000-000000000003");
    private static readonly Guid ModeratorId = Guid.Parse("c0de0000-0000-4000-8000-000000000004");

    private static readonly DateTimeOffset Moment = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTimeOffset Now => Moment;
    }

    [Fact]
    public async Task CountEveryNumberOfTheProfileFromItsOwnRows()
    {
        using var context = Seeded();

        var profile = (await new UserRepository(context, new FixedClock()).GetUserAsync(SubjectId))!;

        profile.Should().BeEquivalentTo(new
        {
            BansReceivedCount = 1,
            EndorsementsGivenCount = 2,
            GameReviewsGivenCount = 3,
            EndorsementsReceivedCount = 4,
            GameReviewsReceivedCount = 5,
            GamesHosting = 6,
            GamesHostingByStatus = new { Draft = 1, Active = 2, Closed = 3 },
            GamesPlaying = 7,
            GamesPlayingByStatus = new { Draft = 1, Active = 2, Closed = 4 },
            BlogsHosting = 8,
            BlogsHostingByStatus = new { Draft = 1, Active = 2, Closed = 5 },
            PostReviewsGivenCount = 9,
            PostReviewsReceivedCount = 10,
            TopicsAuthoredCount = 11,
            CommentsAuthoredCount = 12,
            GlobalChatMessagesCount = 13,
            GameDropsCount = 14,
            PublicationsAuthoredCount = 15,
            LikesReceivedCount = 16,
            SubscribersByCategory = new { Games = 3, Blogs = 2, Topics = 1 },
        });

        // Most recently active first, and the one never active last.
        profile.Subscribers!.Select(s => s.Username).Should().Equal("fan1", "fan2", "fan4", "fan3");
        profile.UsernameHistory!.Select(h => (h.NewUsername, h.ApprovedByUsername)).Should().Equal(
            ("subject", (string?)null),
            ("subject_old", "moderator"));
    }

    /// <summary>
    /// The rule above holds only as long as it names every counter there is.
    /// </summary>
    [Fact]
    public async Task LeaveNoCounterOfTheProfileUnseededOrEqualToAnother()
    {
        using var context = Seeded();

        var profile = (await new UserRepository(context, new FixedClock()).GetUserAsync(SubjectId))!;

        var counters = typeof(GeneralUser).GetProperties()
            .Where(property => property.PropertyType == typeof(int?))
            .ToDictionary(property => property.Name, property => (int?)property.GetValue(profile));

        counters.Should().HaveCountGreaterThanOrEqualTo(16, "the profile carries some sixteen counters");
        counters.Where(counter => counter.Value is null or 0).Select(counter => counter.Key).Should().BeEmpty(
            "a counter left at zero here cannot tell its own rows from nobody's, so the seed has to " +
            "give it some");
        counters.Values.Should().OnlyHaveUniqueItems(
            "two counters at one value cannot tell which dictionary either was read from");
    }

    /// <summary>
    /// The subject, with rows of every kind the profile counts and a row beside
    /// each kind that must not be counted.
    /// </summary>
    private static DmDbContext Seeded()
    {
        var context = new DmDbContext(new DbContextOptionsBuilder<DmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        context.Users.AddRange(
            User(SubjectId, "subject", Moment),
            User(HostId, "host", Moment),
            User(PeerId, "peer", Moment),
            User(ModeratorId, "moderator", Moment));

        SeedHostedGames(context);
        SeedPlayedGames(context);
        SeedBlogs(context);
        SeedReviewsAndEndorsements(context);
        SeedDiscussions(context);
        SeedModeration(context);
        SeedSubscribers(context);

        context.SaveChanges();
        return context;
    }

    /// <summary>Games hosted: three mastered, three assisted, and one of each removed.</summary>
    private static void SeedHostedGames(DmDbContext context)
    {
        context.Games.AddRange(
            Game("mastered-draft", SubjectId, ModuleStatus.Draft),
            Game("mastered-active", SubjectId, ModuleStatus.Active),
            Game("mastered-closed", SubjectId, ModuleStatus.Closed),
            Game("mastered-removed", SubjectId, ModuleStatus.Active, isRemoved: true));

        foreach (var (name, status, isRemoved) in new[]
                 {
                     ("assisted-active", ModuleStatus.Active, false),
                     ("assisted-closed-1", ModuleStatus.Closed, false),
                     ("assisted-closed-2", ModuleStatus.Closed, false),
                     ("assisted-removed", ModuleStatus.Active, true),
                 })
        {
            var game = Game(name, HostId, status, isRemoved);
            context.Games.Add(game);
            context.Set<DbGameAssistant>().Add(new DbGameAssistant
            {
                GameAssistantId = Guid.NewGuid(),
                GameId = game.GameId,
                UserId = SubjectId,
                JoinedUtc = Moment,
            });
        }
    }

    /// <summary>
    /// Games played: seven of them, each with two characters the player walked
    /// away from, and the characters that are not a game played or not a drop.
    /// </summary>
    private static void SeedPlayedGames(DmDbContext context)
    {
        var statuses = new[]
        {
            ModuleStatus.Draft, ModuleStatus.Active, ModuleStatus.Active,
            ModuleStatus.Closed, ModuleStatus.Closed, ModuleStatus.Closed, ModuleStatus.Closed,
        };

        var played = statuses.Select((status, index) => Game($"played-{index}", HostId, status)).ToList();
        context.Games.AddRange(played);
        foreach (var game in played)
        {
            context.Characters.AddRange(Drop(game), Drop(game));
        }

        // Dead rather than gone: a game played, and not a drop.
        context.Characters.Add(Character(played[1], CharacterStatus.Retired, isDead: true));

        // An NPC is not a game played, a removed character is not one either,
        // and a game that is gone takes its drops with it.
        var npcGame = Game("npc-only", HostId, ModuleStatus.Active);
        var removedCharacterGame = Game("removed-character", HostId, ModuleStatus.Active);
        var goneGame = Game("gone", HostId, ModuleStatus.Active, isRemoved: true);
        context.Games.AddRange(npcGame, removedCharacterGame, goneGame);

        var npc = Character(npcGame, CharacterStatus.Active);
        npc.IsNpc = true;
        var removed = Character(removedCharacterGame, CharacterStatus.Active);
        removed.IsRemoved = true;
        context.Characters.AddRange(npc, removed, Drop(goneGame));
    }

    /// <summary>Blogs hosted: two owned, six assisted, and a removed one owned.</summary>
    private static void SeedBlogs(DmDbContext context)
    {
        context.Blogs.AddRange(
            Blog("owned-draft", SubjectId, ModuleStatus.Draft),
            Blog("owned-active", SubjectId, ModuleStatus.Active),
            Blog("owned-removed", SubjectId, ModuleStatus.Active, isRemoved: true));

        var assisted = new[] { ModuleStatus.Active }
            .Concat(Enumerable.Repeat(ModuleStatus.Closed, 5))
            .Select((status, index) => Blog($"assisted-{index}", PeerId, status))
            .ToList();
        context.Blogs.AddRange(assisted);
        context.Set<DbBlogAssistant>().AddRange(assisted.Select(blog => new DbBlogAssistant
        {
            BlogAssistantId = Guid.NewGuid(),
            BlogId = blog.BlogId,
            UserId = SubjectId,
            JoinedUtc = Moment,
        }));

        var home = context.Blogs.Local.Single(blog => blog.PublicId == "owned-active");
        context.Set<DbPublication>().AddRange(Repeat(15, () => Publication(home)));
        var removedPublication = Publication(home);
        removedPublication.IsRemoved = true;
        context.Set<DbPublication>().Add(removedPublication);
    }

    private static void SeedReviewsAndEndorsements(DmDbContext context)
    {
        var mastered = context.Games.Local.Single(game => game.PublicId == "mastered-active");
        var masteredRemoved = context.Games.Local.Single(game => game.PublicId == "mastered-removed");
        var hosts = context.Games.Local.Single(game => game.PublicId == "played-1");

        // Received: about the games the subject masters, and not about a removed one.
        context.GameReviews.AddRange(Repeat(5, () => GameReview(PeerId, mastered)));
        context.GameReviews.AddRange(
            GameReview(PeerId, mastered, isRemoved: true),
            GameReview(PeerId, masteredRemoved));

        // Given: written by the subject, about somebody else's game.
        context.GameReviews.AddRange(Repeat(3, () => GameReview(SubjectId, hosts)));
        context.GameReviews.Add(GameReview(SubjectId, hosts, isRemoved: true));

        context.UserEndorsements.AddRange(Repeat(4, () => Endorsement(PeerId, SubjectId)));
        context.UserEndorsements.Add(Endorsement(PeerId, SubjectId, isRemoved: true));
        context.UserEndorsements.AddRange(Repeat(2, () => Endorsement(SubjectId, PeerId)));
        context.UserEndorsements.Add(Endorsement(SubjectId, PeerId, isRemoved: true));

        context.PostReviews.AddRange(Repeat(9, () => PostReview(SubjectId, PeerId, hosts)));
        context.PostReviews.Add(PostReview(SubjectId, PeerId, hosts, isRemoved: true));
        context.PostReviews.AddRange(Repeat(10, () => PostReview(PeerId, SubjectId, hosts)));
        context.PostReviews.Add(PostReview(PeerId, SubjectId, hosts, isRemoved: true));
    }

    /// <summary>
    /// Topics, comments and global chat lines by the subject, and the likes on
    /// them: one on a topic, two on publications, three on comments and ten on
    /// chat lines.
    /// </summary>
    private static void SeedDiscussions(DmDbContext context)
    {
        var boardId = Guid.NewGuid();
        var topics = Repeat(11, () => Topic(SubjectId, boardId)).ToList();
        context.Topics.AddRange(topics);
        var removedTopic = Topic(SubjectId, boardId);
        removedTopic.IsRemoved = true;
        var peersTopic = Topic(PeerId, boardId);
        context.Topics.AddRange(removedTopic, peersTopic);

        var comments = Repeat(12, () => Comment(SubjectId, topics[0].TopicId)).ToList();
        context.Comments.AddRange(comments);
        var removedComment = Comment(SubjectId, topics[0].TopicId);
        removedComment.IsRemoved = true;
        context.Comments.Add(removedComment);

        var chatLines = Repeat(13, () => Message(SubjectId, DbChat.GlobalChatId)).ToList();
        context.Messages.AddRange(chatLines);
        var removedLine = Message(SubjectId, DbChat.GlobalChatId);
        removedLine.IsRemoved = true;
        context.Messages.AddRange(removedLine, Message(SubjectId, Guid.NewGuid()));

        var publications = context.Set<DbPublication>().Local
            .Where(publication => !publication.IsRemoved)
            .ToList();

        context.Set<DbLike>().AddRange(
            new[] { Like(LikeEntityType.Topic, topics[0].TopicId) }
                .Concat(publications.Take(2).Select(p => Like(LikeEntityType.Publication, p.PublicationId)))
                .Concat(comments.Take(3).Select(c => Like(LikeEntityType.Comment, c.CommentId)))
                .Concat(chatLines.Take(10).Select(m => Like(LikeEntityType.Message, m.MessageId))));

        // A taken-back like, and a like on somebody else's topic.
        var takenBack = Like(LikeEntityType.Topic, topics[1].TopicId);
        takenBack.IsRemoved = true;
        context.Set<DbLike>().AddRange(takenBack, Like(LikeEntityType.Topic, peersTopic.TopicId));
    }

    /// <summary>A ban served and a ban lifted early, and two renames of the subject.</summary>
    private static void SeedModeration(DmDbContext context)
    {
        context.Set<DbBan>().AddRange(
            Ban(liftedUtc: null),
            Ban(liftedUtc: Moment.AddDays(-20)));

        context.Set<DbUsernameHistory>().AddRange(
            new DbUsernameHistory
            {
                UsernameHistoryId = Guid.NewGuid(),
                UserId = SubjectId,
                OldUsername = "subject_first",
                NewUsername = "subject_old",
                ChangedUtc = Moment.AddDays(-10),
                ApprovedByUserId = ModeratorId,
            },
            new DbUsernameHistory
            {
                UsernameHistoryId = Guid.NewGuid(),
                UserId = SubjectId,
                OldUsername = "subject_old",
                NewUsername = "subject",
                ChangedUtc = Moment.AddDays(-1),
            });
    }

    /// <summary>
    /// Four subscribers, three to the games, two to the blogs, one to the topics;
    /// one of them was never active.
    /// </summary>
    private static void SeedSubscribers(DmDbContext context)
    {
        var fans = new[]
        {
            ("fan1", (DateTimeOffset?)Moment.AddHours(-1),
                SubscriptionSettings.AuthorGameEvents | SubscriptionSettings.AuthorBlogEvents |
                SubscriptionSettings.AuthorTopicEvents),
            ("fan2", Moment.AddHours(-2), SubscriptionSettings.AuthorGameEvents | SubscriptionSettings.AuthorBlogEvents),
            ("fan3", null, SubscriptionSettings.AuthorGameEvents),
            ("fan4", Moment.AddHours(-3), SubscriptionSettings.None),
        };

        foreach (var (username, lastActivity, settings) in fans)
        {
            var fan = User(Guid.NewGuid(), username, lastActivity);
            context.Users.Add(fan);
            context.Subscriptions.Add(new DbSubscription
            {
                SubscriptionId = Guid.NewGuid(),
                SubscriberId = fan.UserId,
                TargetType = SubscriptionTargetType.User,
                TargetId = SubjectId,
                Settings = settings,
                CreatedUtc = Moment,
            });
        }
    }

    private static IEnumerable<T> Repeat<T>(int count, Func<T> create) =>
        Enumerable.Range(0, count).Select(_ => create()).ToList();

    private static DbUser User(Guid id, string username, DateTimeOffset? lastActivity) => new()
    {
        UserId = id,
        Username = username,
        Email = $"{username}@test.local",
        Salt = "",
        PasswordHash = "",
        Role = UserRole.RegularUser,
        CreatedUtc = Moment.AddYears(-1),
        LastActivityUtc = lastActivity,
    };

    private static DbGame Game(string publicId, Guid masterId, ModuleStatus status, bool isRemoved = false) => new()
    {
        GameId = Guid.NewGuid(),
        PublicId = publicId,
        MasterId = masterId,
        Title = publicId,
        Status = status,
        CreatedUtc = Moment.AddMonths(-1),
        IsRemoved = isRemoved,
    };

    private static DbCharacter Character(DbGame game, CharacterStatus status, bool isDead = false) => new()
    {
        CharacterId = Guid.NewGuid(),
        GameId = game.GameId,
        AuthorId = SubjectId,
        Status = status,
        IsDead = isDead,
        Name = "Персонаж",
        CreatedUtc = Moment.AddDays(-20),
    };

    /// <summary>A character the player retired by leaving the game.</summary>
    private static DbCharacter Drop(DbGame game)
    {
        var character = Character(game, CharacterStatus.Retired);
        character.IsPlayerLeft = true;
        return character;
    }

    private static DbBlog Blog(string publicId, Guid authorId, ModuleStatus status, bool isRemoved = false) => new()
    {
        BlogId = Guid.NewGuid(),
        PublicId = publicId,
        AuthorId = authorId,
        Title = publicId,
        Status = status,
        CreatedUtc = Moment.AddMonths(-1),
        IsRemoved = isRemoved,
    };

    private static DbPublication Publication(DbBlog blog) => new()
    {
        PublicationId = Guid.NewGuid(),
        BlogId = blog.BlogId,
        AuthorId = SubjectId,
        Title = "Публикация",
        Content = "Текст",
        CreatedUtc = Moment.AddDays(-5),
    };

    private static DbGameReview GameReview(Guid authorId, DbGame game, bool isRemoved = false) => new()
    {
        GameReviewId = Guid.NewGuid(),
        AuthorId = authorId,
        GameId = game.GameId,
        Text = "Отзыв",
        CreatedUtc = Moment.AddDays(-3),
        IsRemoved = isRemoved,
    };

    private static DbEndorsement Endorsement(Guid authorId, Guid targetId, bool isRemoved = false) => new()
    {
        UserEndorsementId = Guid.NewGuid(),
        AuthorId = authorId,
        TargetUserId = targetId,
        Text = "Рекомендация",
        CreatedUtc = Moment.AddDays(-3),
        IsRemoved = isRemoved,
    };

    private static DbPostReview PostReview(Guid authorId, Guid postAuthorId, DbGame game, bool isRemoved = false) => new()
    {
        PostReviewId = Guid.NewGuid(),
        AuthorId = authorId,
        PostAuthorId = postAuthorId,
        PostId = Guid.NewGuid(),
        GameId = game.GameId,
        SignValue = 1,
        CreatedUtc = Moment.AddDays(-3),
        IsRemoved = isRemoved,
    };

    private static DbTopic Topic(Guid authorId, Guid boardId) => new()
    {
        TopicId = Guid.NewGuid(),
        BoardId = boardId,
        AuthorId = authorId,
        Title = "Тема",
        Text = "Первое сообщение",
        CreatedUtc = Moment.AddDays(-7),
    };

    private static DbComment Comment(Guid authorId, Guid entityId) => new()
    {
        CommentId = Guid.NewGuid(),
        EntityId = entityId,
        AuthorId = authorId,
        Text = "Реплика",
        CreatedUtc = Moment.AddDays(-6),
    };

    private static DbMessage Message(Guid userId, Guid chatId) => new()
    {
        MessageId = Guid.NewGuid(),
        UserId = userId,
        ChatId = chatId,
        Text = "Строка чата",
        CreatedUtc = Moment.AddDays(-2),
    };

    private static DbLike Like(LikeEntityType type, Guid entityId) => new()
    {
        LikeId = Guid.NewGuid(),
        EntityType = type,
        EntityId = entityId,
        UserId = PeerId,
    };

    private static DbBan Ban(DateTimeOffset? liftedUtc) => new()
    {
        BanId = Guid.NewGuid(),
        TargetUserId = SubjectId,
        AuthorId = ModeratorId,
        StartedUtc = Moment.AddDays(-30),
        EndedUtc = Moment.AddDays(30),
        Comment = "Нарушение",
        AccessRestrictionPolicy = AccessPolicy.FullBan,
        LiftedByUserId = liftedUtc is null ? null : ModeratorId,
        LiftedUtc = liftedUtc,
    };
}
