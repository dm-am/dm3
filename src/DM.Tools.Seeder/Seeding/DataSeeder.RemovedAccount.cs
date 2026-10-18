using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.InitialData;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>The login of the seeded removed account.</summary>
    private const string RemovedAccountLogin = "Ушедший Мастер";

    /// <summary>
    /// One removed account that left something everywhere the profile of "Аккаунт
    /// удален" reads: a topic with a comment on an open board, a game it mastered with a
    /// rated post in an open room, and a character with a post in somebody else's game.
    /// </summary>
    /// <remarks>
    /// Nothing else in the seed is removed, so without this the profile of removed
    /// accounts comes up empty on a fresh stand and every signature "Аккаунт удален" the
    /// site can draw has nothing to draw it on. Written live and removed at the end, the
    /// way an account leaves: the removal sets the flag and touches nothing it wrote.
    ///
    /// Idempotent by the login, looked up past the soft-delete filter because the row it
    /// finds is a removed one.
    /// </remarks>
    private async Task SeedRemovedAccount(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var exists = await _dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Username.ToLower() == RemovedAccountLogin.ToLower());
        if (exists)
        {
            result.Details.Add("Removed account already seeded, skipping");
            return;
        }

        var reviewer = users.FirstOrDefault(u => u.Username == "TestUser") ?? users.First();

        var board = (await _dbContext.Set<Board>()
                .Where(b => (b.ViewPolicy & BoardAccessPolicy.Guest) != BoardAccessPolicy.None)
                .ToListAsync())
            .OrderBy(b => b.Order)
            .ThenBy(b => b.Alias, StringComparer.Ordinal)
            .FirstOrDefault();
        var hostGame = (await _dbContext.Set<DbGame>()
                .Where(g => g.Status == ModuleStatus.Active &&
                            g.PremoderationStatus == PremoderationStatus.Approved &&
                            g.Rooms.Any(r => r.AccessType == RoomAccessType.Open && !r.IsRemoved))
                .ToListAsync())
            .OrderBy(g => g.SerialNumber)
            .FirstOrDefault();
        if (board == null || hostGame == null)
        {
            result.Details.Add("Removed account skipped: no open board or no active game with an open room");
            return;
        }

        var (hash, salt) = _securityManager.GeneratePassword("Test123!");
        var account = new DbUser
        {
            UserId = _guidFactory.Create(),
            Username = RemovedAccountLogin,
            Email = "removed@test.local",
            PasswordHash = hash,
            Salt = salt,
            PasswordHashVersion = 2,
            Role = UserRole.RegularUser,
            CreatedUtc = now.AddYears(-5),
            LastActivityUtc = now.AddDays(-60),
            Status = "Ухожу, но истории остаются",
            Name = string.Empty,
            Location = string.Empty,
            Info = string.Empty,
            IsRemoved = false,
        };
        _dbContext.Users.Add(account);

        // The topic, backdated below the board's newest so its "last topic" stays.
        var topicNumber = await _dbContext.Set<Topic>()
            .IgnoreQueryFilters()
            .Where(t => t.BoardId == board.BoardId)
            .MaxAsync(t => (int?)t.TopicNumber) ?? 0;
        var topic = new Topic
        {
            TopicId = _guidFactory.Create(),
            BoardId = board.BoardId,
            AuthorId = account.UserId,
            TopicNumber = topicNumber + 1,
            Title = "Последний сбор у старого маяка",
            Text = "Друзья, это мой последний сбор. Кто помнит маяк на мысе, приходите: " +
                   "расскажем друг другу, чем закончились наши истории.",
            CreatedUtc = now.AddDays(-75),
        };
        _dbContext.Set<Topic>().Add(topic);
        board.TopicsCount++;
        var comment = new DbComment
        {
            CommentId = _guidFactory.Create(),
            EntityId = topic.TopicId,
            AuthorId = account.UserId,
            CreatedUtc = now.AddDays(-74),
            Text = "Маяк на месте, огонь зажжен. Жду всех к полуночи.",
        };
        _dbContext.Set<DbComment>().Add(comment);
        result.TopicsCreated++;
        result.CommentsCreated++;

        // The game it mastered, with a post in an open room that another reader rated.
        var gameSerialNumber = await SerialNumberAllocator.NextAsync<DbGame>(_dbContext);
        var game = new DbGame
        {
            GameId = _guidFactory.Create(),
            SerialNumber = gameSerialNumber,
            PublicId = _publicIdService.Encode(gameSerialNumber),
            CreatedUtc = now.AddDays(-120),
            ActivatedUtc = now.AddDays(-118),
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            DraftVisibility = DraftVisibility.Public,
            MasterId = account.UserId,
            Title = "Хроники старого маяка",
            SystemName = "Словеска",
            NarrativeSetting = "Приморский городок",
            Info = "Игра мастера, который ушел с сайта. Все, что здесь написано, осталось на месте.",
            CommentsAccessMode = CommentsAccessMode.Public,
            LastPostCreatedUtc = now.AddDays(-65),
        };
        _dbContext.Set<DbGame>().Add(game);
        _dbContext.GameAttributeSchemas.Add(new DbGameAttributeSchema
        {
            GameId = game.GameId,
            Specifications = DefaultAttributeSchema.Specifications(),
            Version = 0,
            UpdatedUtc = game.CreatedUtc,
        });
        var room = new Room
        {
            RoomId = _guidFactory.Create(),
            GameId = game.GameId,
            Title = "Маяк",
            AccessType = RoomAccessType.Open,
            Type = RoomType.Default,
            RoomNumber = 1,
            OrderNumber = 1,
            ViewPrivateText = false,
            ViewDiceResults = true,
            DiceEnabled = true,
        };
        _dbContext.Set<Room>().Add(room);
        var ratedPost = new Post
        {
            PostId = _guidFactory.Create(),
            RoomId = room.RoomId,
            AuthorId = account.UserId,
            CreatedUtc = now.AddDays(-66),
            GameText = "Туман стелется по камням, и маяк впервые за много лет гаснет. " +
                       "Кто-то должен подняться наверх и снова зажечь огонь.",
        };
        _dbContext.Set<Post>().Add(ratedPost);
        _dbContext.Set<Post>().Add(new Post
        {
            PostId = _guidFactory.Create(),
            RoomId = room.RoomId,
            AuthorId = account.UserId,
            CreatedUtc = now.AddDays(-65),
            GameText = "Огонь горит. Корабли видят берег, а мастер уходит дальше по тропе.",
        });
        _dbContext.PostReviews.Add(new DbPostReview
        {
            PostReviewId = _guidFactory.Create(),
            PostId = ratedPost.PostId,
            PostAuthorId = account.UserId,
            AuthorId = reviewer.UserId,
            GameId = game.GameId,
            SignValue = 1,
            Text = "Лучший пост этой игры.",
            CreatedUtc = now.AddDays(-64),
        });
        result.GamesCreated++;
        result.PostsCreated += 2;

        // A character in somebody else's game, with one post in its open room.
        var hostRoom = await _dbContext.Set<Room>()
            .Where(r => r.GameId == hostGame.GameId && r.AccessType == RoomAccessType.Open)
            .OrderBy(r => r.RoomNumber)
            .FirstAsync();
        var character = new Character
        {
            CharacterId = _guidFactory.Create(),
            GameId = hostGame.GameId,
            AuthorId = account.UserId,
            Name = "Смотритель Ион",
            Status = CharacterStatus.Active,
            IsNpc = false,
            AccessPolicy = CharacterAccessPolicy.NoAccess,
            CreatedUtc = now.AddDays(-90),
        };
        _dbContext.Set<Character>().Add(character);
        AddLegacyCharacterAttributes(character.CharacterId,
            story: "Смотритель маяка, который однажды не вернулся с ночного дежурства.");
        _dbContext.Set<Post>().Add(new Post
        {
            PostId = _guidFactory.Create(),
            RoomId = hostRoom.RoomId,
            CharacterId = character.CharacterId,
            AuthorId = account.UserId,
            CreatedUtc = now.AddDays(-80),
            GameText = "Ион молча кивает и уходит к берегу, туда, где всегда горит свет.",
        });
        result.CharactersCreated++;
        result.PostsCreated++;

        // The public counters the row carries, as the ones the run recomputed for the
        // live accounts: three posts written, one point from the one review.
        account.QuantityRating = 3;
        account.QualityRating = 1;

        await _dbContext.SaveChangesAsync();

        topic.LastCommentId = comment.CommentId;
        account.IsRemoved = true;
        await _dbContext.SaveChangesAsync();

        result.Details.Add(
            $"Removed account \"{RemovedAccountLogin}\" seeded: a topic, a game it mastered, a rated post, " +
            "a character in another game");
    }
}
