using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DbBlogBlacklist = DM.Infrastructure.Persistence.Entities.Blog.BlogBlacklist;
using DbBoard = DM.Infrastructure.Persistence.Entities.Forum.Board;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbRoomAccess = DM.Infrastructure.Persistence.Entities.Game.Links.RoomAccess;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbGameBlacklist = DM.Infrastructure.Persistence.Entities.Game.Links.GameBlacklist;
using DbGameReview = DM.Infrastructure.Persistence.Entities.Game.GameReview;
using DbLike = DM.Infrastructure.Persistence.Entities.Shared.Like;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPostEdit = DM.Infrastructure.Persistence.Entities.Game.Posts.PostEdit;
using DbPostPendency = DM.Infrastructure.Persistence.Entities.Game.Links.PostPendency;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbSubscription = DM.Infrastructure.Persistence.Entities.Subscriptions.Subscription;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbUserBlacklist = DM.Infrastructure.Persistence.Entities.Account.UserBlacklist;

namespace DM.Web.API.IntegrationTests.Controllers.General;

public partial class BlacklistRuleShould
{
    /// <summary>
    /// A blacklisted user and what he holds in the game and the blog before the
    /// entries.
    /// </summary>
    /// <remarks>
    /// Everything the closures could touch in the game and the blog, plus a personal list entry and a topic; a
    /// review, a rating and the like are left out of rows that put the first one, being one per game or comment.
    /// </remarks>
    private sealed record Seed(GeneralUser User)
    {
        public Guid GameCommentId { get; } = Guid.NewGuid();
        public Guid OtherGameCommentId { get; } = Guid.NewGuid();
        public Guid LikeId { get; } = Guid.NewGuid();
        public Guid ReviewId { get; } = Guid.NewGuid();
        public Guid RatingId { get; } = Guid.NewGuid();
        public Guid ApplicationId { get; } = Guid.NewGuid();
        public Guid CharacterId { get; } = Guid.NewGuid();
        public Guid RetiredCharacterId { get; } = Guid.NewGuid();
        public Guid RoomId { get; } = Guid.NewGuid();
        public Guid PostId { get; } = Guid.NewGuid();
        public Guid ChatRoomId { get; } = Guid.NewGuid();
        public Guid ChatId { get; } = Guid.NewGuid();
        public Guid BlogCommentId { get; } = Guid.NewGuid();
        public Guid OtherBlogCommentId { get; } = Guid.NewGuid();
        public Guid PublicationId { get; } = Guid.NewGuid();
        public Guid PublicationCommentId { get; } = Guid.NewGuid();
        public Guid OtherPublicationCommentId { get; } = Guid.NewGuid();
        public Guid BoardId { get; } = Guid.NewGuid();
        public Guid TopicId { get; } = Guid.NewGuid();
        public Guid TopicCommentId { get; } = Guid.NewGuid();

        /// <summary>A user the case made and nothing else, for the drop.</summary>
        public static Seed Only(GeneralUser user) => new(user);
    }

    private async Task<Seed> SeedBlacklistedAsync(string? action = null)
    {
        // Past the probation, so a review is the newcomer rule's to refuse.
        var user = await SeedUserAsync("blr-x", quantityRating: 200);
        var now = DateTimeOffset.UtcNow;
        var seed = new Seed(user);

        await using var db = DatabaseFixture.CreateDbContext();
        AddLists(db, user, now);

        // The game.
        AddGameDiscussionAndFeedback(db, seed, action, now);
        db.Set<DbCharacter>().AddRange(
            Character(seed.ApplicationId, user.UserId, "Заявка из черного списка", CharacterStatus.UnderReview, now),
            Character(seed.CharacterId, user.UserId, "Персонаж в игре", CharacterStatus.Active, now),
            Character(seed.RetiredCharacterId, user.UserId, "Ушедший персонаж", CharacterStatus.Retired, now));
        AddRooms(db, seed, now);

        // The blog.
        AddBlogDiscussion(db, seed, now);

        // The forum.
        AddForumTopic(db, seed, now);
        await db.SaveChangesAsync();
        return seed;
    }

    /// <summary>The user on the game's list, the blog's list and their owner's personal list.</summary>
    private static void AddLists(DmDbContext db, GeneralUser user, DateTimeOffset now)
    {
        var game = TestConstants.TestGameId;
        var blog = TestConstants.TestBlogId;
        db.GameBlacklists.Add(new DbGameBlacklist
        {
            EntryId = Guid.NewGuid(),
            GameId = game,
            BlockedUserId = user.UserId,
            BlockedByUserId = TestConstants.TestUserId,
            CreatedUtc = now
        });
        db.BlogBlacklists.Add(new DbBlogBlacklist
        {
            EntryId = Guid.NewGuid(),
            BlogId = blog,
            BlockedUserId = user.UserId,
            BlockedByUserId = TestConstants.TestUserId,
            CreatedUtc = now
        });
        db.UserBlacklists.Add(new DbUserBlacklist
        {
            EntryId = Guid.NewGuid(),
            OwnerId = TestConstants.TestUserId,
            BlockedUserId = user.UserId,
            CreatedUtc = now
        });
    }

    /// <summary>
    /// His comment and somebody else's in the game's discussion, and the like, the
    /// review and the rating he left before the list.
    /// </summary>
    private static void AddGameDiscussionAndFeedback(DmDbContext db, Seed seed, string? action, DateTimeOffset now)
    {
        var user = seed.User;
        var game = TestConstants.TestGameId;
        var other = TestConstants.SecondUserId;
        db.Comments.AddRange(
            Comment(seed.GameCommentId, game, user.UserId, now),
            Comment(seed.OtherGameCommentId, game, other, now));
        if (action != "like a comment in the game")
        {
            db.Likes.Add(new DbLike
            {
                LikeId = seed.LikeId,
                EntityId = seed.OtherGameCommentId,
                EntityType = LikeEntityType.Comment,
                UserId = user.UserId
            });
        }
        if (action != "review the game")
        {
            db.GameReviews.Add(new DbGameReview
            {
                GameReviewId = seed.ReviewId,
                AuthorId = user.UserId,
                GameId = game,
                CreatedUtc = now,
                Text = "Рецензия, написанная до черного списка",
                IsRemoved = false
            });
        }

        if (action != "rate a post of the game")
        {
            db.PostReviews.Add(new DbPostReview
            {
                PostReviewId = seed.RatingId,
                AuthorId = user.UserId,
                PostId = TestConstants.TestGamePostId,
                PostAuthorId = TestConstants.TestUserId,
                GameId = game,
                CreatedUtc = now,
                Text = null,
                SignValue = 0,
                IsRemoved = false
            });
        }
    }

    /// <summary>
    /// The room both his characters are seated in, with his post, and the chat
    /// room he writes in as a reader.
    /// </summary>
    private static void AddRooms(DmDbContext db, Seed seed, DateTimeOffset now)
    {
        var user = seed.User;
        var game = TestConstants.TestGameId;
        var roomNumber = 1000 + Random.Shared.Next(100_000);
        db.Set<DbRoom>().AddRange(
            new DbRoom
            {
                RoomId = seed.RoomId,
                GameId = game,
                RoomNumber = roomNumber,
                Title = "Комната внесенного",
                AccessType = RoomAccessType.Open,
                Type = RoomType.Default,
                OrderNumber = 1000.0,
                IsRemoved = false
            },
            new DbRoom
            {
                RoomId = seed.ChatRoomId,
                GameId = game,
                RoomNumber = roomNumber + 1,
                Title = "Чат-комната внесенного",
                AccessType = RoomAccessType.Private,
                Type = RoomType.Chat,
                OrderNumber = 1001.0,
                ChatId = seed.ChatId,
                IsRemoved = false
            });
        db.Chats.Add(new DbChat
        {
            ChatId = seed.ChatId,
            Type = ChatType.GameRoom,
            Title = "Чат-комната внесенного",
            RoomId = seed.ChatRoomId,
            PublicId = $"b{seed.ChatId:N}"[..10]
        });
        db.Set<DbRoomAccess>().AddRange(
            new DbRoomAccess
            {
                AccessId = Guid.NewGuid(),
                CharacterId = seed.CharacterId,
                RoomId = seed.RoomId,
                Policy = RoomAccessPolicy.Full
            },
            new DbRoomAccess
            {
                AccessId = Guid.NewGuid(),
                CharacterId = seed.RetiredCharacterId,
                RoomId = seed.RoomId,
                Policy = RoomAccessPolicy.Full
            },
            new DbRoomAccess
            {
                AccessId = Guid.NewGuid(),
                ReaderUserId = user.UserId,
                RoomId = seed.ChatRoomId,
                Policy = RoomAccessPolicy.Full
            });
        db.Set<DbPost>().Add(new DbPost
        {
            PostId = seed.PostId,
            RoomId = seed.RoomId,
            CharacterId = seed.CharacterId,
            AuthorId = user.UserId,
            CreatedUtc = now.AddMinutes(-1),
            GameText = "Пост, написанный до черного списка",
            PrivateAddresseeSnapshotJson = "{}",
            IsRemoved = false
        });
    }

    /// <summary>
    /// Somebody else's publication, and a comment of his and one of somebody else
    /// both in the blog's discussion and under the publication.
    /// </summary>
    private static void AddBlogDiscussion(DmDbContext db, Seed seed, DateTimeOffset now)
    {
        var user = seed.User;
        var blog = TestConstants.TestBlogId;
        var other = TestConstants.SecondUserId;
        db.Publications.Add(new DbPublication
        {
            PublicationId = seed.PublicationId,
            BlogId = blog,
            PublicationNumber = 9000 + Random.Shared.Next(100_000),
            AuthorId = TestConstants.TestUserId,
            Title = "Публикация в блоге со списком",
            Content = "Текст",
            Preview = "Текст",
            CreatedUtc = now,
            IsRemoved = false
        });
        db.Comments.AddRange(
            Comment(seed.BlogCommentId, blog, user.UserId, now),
            Comment(seed.OtherBlogCommentId, blog, other, now),
            Comment(seed.PublicationCommentId, seed.PublicationId, user.UserId, now),
            Comment(seed.OtherPublicationCommentId, seed.PublicationId, other, now));
    }

    /// <summary>A board of the case's own with the owner's topic and comment in it.</summary>
    private static void AddForumTopic(DmDbContext db, Seed seed, DateTimeOffset now)
    {
        db.Boards.Add(new DbBoard
        {
            BoardId = seed.BoardId,
            Title = $"Blacklist board {seed.BoardId:N}",
            Alias = $"blr-{seed.BoardId:N}",
            Order = 0,
            CreateTopicPolicy = BoardAccessPolicy.Guest
        });
        db.Topics.Add(new DbTopic
        {
            TopicId = seed.TopicId,
            BoardId = seed.BoardId,
            AuthorId = TestConstants.TestUserId,
            Title = "Топик того, кто внес",
            Text = "Текст топика того, кто внес",
            TopicNumber = 1,
            CreatedUtc = now.AddHours(-1),
            IsRemoved = false,
            IsAttached = false,
            IsClosed = false
        });
        db.Comments.Add(Comment(seed.TopicCommentId, seed.TopicId, TestConstants.TestUserId, now));
    }

    private async Task<GeneralUser> SeedUserAsync(
        string prefix, UserBlacklistSettings settings = UserBlacklistSettings.Default, int quantityRating = 0)
    {
        var userId = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"{prefix}{userId:N}"[..20],
            Email = $"{userId:N}@blacklistrule.example",
            PasswordHash = "hash",
            Salt = "salt",
            BlacklistSettings = settings,
            QuantityRating = quantityRating
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new GeneralUser { UserId = userId, Username = user.Username, Role = UserRole.RegularUser };
    }

    private static DbComment Comment(Guid commentId, Guid entityId, Guid authorId, DateTimeOffset now) => new()
    {
        CommentId = commentId,
        EntityId = entityId,
        AuthorId = authorId,
        CreatedUtc = now.AddMinutes(-1),
        Text = "Комментарий, написанный до черного списка",
        IsRemoved = false
    };

    private static DbCharacter Character(
        Guid characterId, Guid authorId, string name, CharacterStatus status, DateTimeOffset now) => new()
        {
            CharacterId = characterId,
            GameId = TestConstants.TestGameId,
            AuthorId = authorId,
            Name = name,
            Status = status,
            IsPlayerLeft = status == CharacterStatus.Retired,
            IsNpc = false,
            AccessPolicy = CharacterAccessPolicy.NoAccess,
            CreatedUtc = now.AddDays(-1),
            IsRemoved = false
        };

    private static DbSubscription Subscription(Guid subscriberId, SubscriptionTargetType type, Guid targetId) => new()
    {
        SubscriptionId = Guid.NewGuid(),
        SubscriberId = subscriberId,
        TargetType = type,
        TargetId = targetId,
        Settings = SubscriptionSettings.InApp,
        CreatedUtc = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Everything the case wrote, or its requests wrote, under the user it made.
    /// The user row stays, the way the neighbouring suites leave theirs.
    /// </summary>
    private async Task DropAsync(Seed seed)
    {
        var userId = seed.User.UserId;
        var rooms = new[] { seed.RoomId, seed.ChatRoomId };
        var commented = new[] { seed.OtherGameCommentId, seed.OtherBlogCommentId, seed.OtherPublicationCommentId };
        await using var db = DatabaseFixture.CreateDbContext();
        await db.Likes.IgnoreQueryFilters()
            .Where(l => l.UserId == userId || commented.Contains(l.EntityId)).ExecuteDeleteAsync();
        await db.Topics.IgnoreQueryFilters().Where(t => t.TopicId == seed.TopicId)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.LastCommentId, (Guid?)null));
        await db.Comments.IgnoreQueryFilters()
            .Where(c => c.AuthorId == userId || commented.Contains(c.CommentId) || c.EntityId == seed.TopicId ||
                        c.EntityId == seed.PublicationId)
            .ExecuteDeleteAsync();
        await db.Publications.IgnoreQueryFilters().Where(p => p.PublicationId == seed.PublicationId)
            .ExecuteDeleteAsync();
        await db.Set<DbPostPendency>().Where(p => rooms.Contains(p.RoomId)).ExecuteDeleteAsync();
        await db.Set<DbPostEdit>().Where(e => db.Set<DbPost>().IgnoreQueryFilters()
            .Any(p => p.PostId == e.PostId && p.AuthorId == userId)).ExecuteDeleteAsync();
        await db.PostReviews.IgnoreQueryFilters().Where(r => r.AuthorId == userId).ExecuteDeleteAsync();
        await db.Set<DbPost>().IgnoreQueryFilters().Where(post => post.AuthorId == userId).ExecuteDeleteAsync();
        await db.Set<DbRoomAccess>().Where(a => rooms.Contains(a.RoomId)).ExecuteDeleteAsync();
        await db.Chats.Where(c => c.ChatId == seed.ChatId)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.LastMessageId, (Guid?)null));
        await db.Set<DbMessage>().IgnoreQueryFilters().Where(m => m.ChatId == seed.ChatId).ExecuteDeleteAsync();
        await db.Set<DbRoom>().IgnoreQueryFilters().Where(r => rooms.Contains(r.RoomId)).ExecuteDeleteAsync();
        await db.Chats.Where(c => c.ChatId == seed.ChatId).ExecuteDeleteAsync();
        await db.Set<DbCharacter>().IgnoreQueryFilters().Where(c => c.AuthorId == userId).ExecuteDeleteAsync();
        await db.Boards.Where(b => b.BoardId == seed.BoardId)
            .ExecuteUpdateAsync(b => b.SetProperty(x => x.LastTopicId, (Guid?)null));
        await db.Topics.IgnoreQueryFilters().Where(t => t.BoardId == seed.BoardId).ExecuteDeleteAsync();
        await db.Boards.Where(b => b.BoardId == seed.BoardId).ExecuteDeleteAsync();
        await db.GameReviews.IgnoreQueryFilters().Where(r => r.AuthorId == userId).ExecuteDeleteAsync();
        await db.Tokens.IgnoreQueryFilters().Where(t => t.UserId == userId).ExecuteDeleteAsync();
        await db.GameBlacklists.Where(b => b.BlockedUserId == userId).ExecuteDeleteAsync();
        await db.BlogBlacklists.Where(b => b.BlockedUserId == userId).ExecuteDeleteAsync();
        await db.UserBlacklists.Where(b => b.BlockedUserId == userId || b.OwnerId == userId).ExecuteDeleteAsync();
        await db.Subscriptions.Where(s => s.SubscriberId == userId).ExecuteDeleteAsync();
    }
}
