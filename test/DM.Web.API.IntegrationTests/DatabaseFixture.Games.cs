using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Game;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;

using DM.Web.API.IntegrationTests.Helpers;
using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;

namespace DM.Web.API.IntegrationTests;

public partial class DatabaseFixture
{
    private static void SeedGames(DmDbContext db)
    {
        if (db.Set<Game>().Any(g => g.GameId == TestConstants.TestGameId)) return;

        // (Id, AuthorId, Title, System, Setting, Info, DaysAgo, ActivatedDaysAgo, IsOpen, Count, Comments)
        var games = new (Guid Id, Guid AuthorId, string Title, string System, string Setting, string Info,
            int CreatedDaysAgo, int? ActivatedDaysAgo, bool IsOpen, int RecruitCount, CommentsAccessMode Comments)[]
        {
            (TestConstants.TestGameId, TestConstants.TestUserId,
                "Test Game", "D&D 5e", "Forgotten Realms", "Test game for integration tests.",
                10, null, true, 1, CommentsAccessMode.Public),
            (TestConstants.SecondGameId, TestConstants.SecondUserId,
                "Second Test Game", "Pathfinder", "Golarion", "Another test game owned by different user.",
                5, null, false, 1, CommentsAccessMode.Readonly),
            (TestConstants.InitialRecruitmentGameId, TestConstants.TestUserId,
                "Initial Recruitment Game", "GURPS", "Fantasy", "Game with first-time recruitment.",
                3, null, true, 1, CommentsAccessMode.Public),
            (TestConstants.SubsequentRecruitmentGameId, TestConstants.TestUserId,
                "Subsequent Recruitment Game", "Fate", "Urban Fantasy", "Game with subsequent recruitment.",
                30, 28, true, 2, CommentsAccessMode.Public),
            (TestConstants.ClosedRecruitmentGameId, TestConstants.SecondUserId,
                "Closed Recruitment Game", "World of Darkness", "Gothic Horror", "Game with closed recruitment.",
                60, 55, false, 2, CommentsAccessMode.Readonly),
            (TestConstants.NoRecruitmentGameId, TestConstants.AdminUserId,
                "Invitation Only Game", "Словеска", "Original", "Game that was always by invitation only.",
                90, 85, false, 0, CommentsAccessMode.Readonly),
        };

        db.Set<Game>().AddRange(games.Select((g, i) => new Game
        {
            GameId = g.Id,
            PublicId = $"game{(char)('a' + i)}",
            MasterId = g.AuthorId,
            Title = g.Title,
            SystemName = g.System,
            NarrativeSetting = g.Setting,
            Info = g.Info,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-g.CreatedDaysAgo),
            ActivatedUtc = g.ActivatedDaysAgo.HasValue ? DateTimeOffset.UtcNow.AddDays(-g.ActivatedDaysAgo.Value) : null,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            IsRecruitmentOpen = g.IsOpen,
            RecruitmentCount = g.RecruitCount,
            RecruitmentStartedUtc = g.IsOpen ? DateTimeOffset.UtcNow.AddDays(-Math.Min(5, g.CreatedDaysAgo)) : null,
            CommentsAccessMode = g.Comments,
            IsRemoved = false
        }));

        // Every writer of a game row writes its sheet in the same save, and a
        // fixture is such a writer: the reads of a game fail loudly without one.
        db.Set<DbGameAttributeSchema>().AddRange(
            GameSheetTestHelper.Sheets(games.Select(g => g.Id)));
    }

    private static void SeedGameTags(DmDbContext db)
    {
        if (db.Set<GameTag>().Any(t => t.GameId == TestConstants.TestGameId)) return;

        static Guid T(string hex) => Guid.Parse($"00000000-0000-0000-0000-0000000000{hex}");

        // GameId -> TagHexes
        var gameTagMap = new Dictionary<Guid, string[]>
        {
            [TestConstants.TestGameId] = ["03", "2b", "1b", "31", "2c", "35"],
            [TestConstants.SecondGameId] = ["0d", "2b", "1c", "31", "34", "35"],
            [TestConstants.InitialRecruitmentGameId] = ["0a", "2b", "2f", "3c", "36"],
            [TestConstants.SubsequentRecruitmentGameId] = ["08", "2b", "22", "23", "31", "33", "3e"],
            [TestConstants.ClosedRecruitmentGameId] = ["15", "29", "22", "27", "31", "34", "39", "35"],
            [TestConstants.NoRecruitmentGameId] = ["18", "2b", "2f", "3a", "3e"],
        };

        db.Set<GameTag>().AddRange(
            gameTagMap.SelectMany(kv => kv.Value.Select(hex => new GameTag
            {
                GameTagId = Guid.NewGuid(),
                GameId = kv.Key,
                TagId = T(hex)
            })));
    }

    private static void SeedRooms(DmDbContext db)
    {
        if (db.Set<Room>().Any(r => r.RoomId == TestConstants.TestRoomId)) return;

        db.Set<Room>().Add(new Room
        {
            RoomId = TestConstants.TestRoomId,
            GameId = TestConstants.TestGameId,
            RoomNumber = 1,
            Title = "Main Room",
            AccessType = RoomAccessType.Open,
            Type = RoomType.Chat,
            OrderNumber = 1.0,
            ViewPrivateText = false,
            ViewDiceResults = true,
            DiceEnabled = true,
            IsRemoved = false
        });
    }

    // One post with a positive review in TestGame's main room, so the rated listing has a row whose
    // GameRef shape the tests assert.
    private static void SeedGamePosts(DmDbContext db)
    {
        if (db.Set<Post>().Any(p => p.PostId == TestConstants.TestGamePostId)) return;

        db.Set<Post>().Add(new Post
        {
            PostId = TestConstants.TestGamePostId,
            RoomId = TestConstants.TestRoomId,
            CharacterId = TestConstants.TestCharacterId,
            AuthorId = TestConstants.TestUserId,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-12),
            GameText = "Test post body (in-character content)",
            MetagameText = null,
            PrivateAddresseeSnapshotJson = "{}",
            IsRemoved = false
        });

        db.Set<PostReview>().Add(new PostReview
        {
            PostReviewId = TestConstants.TestGamePostReviewId,
            AuthorId = TestConstants.SecondUserId,
            PostId = TestConstants.TestGamePostId,
            PostAuthorId = TestConstants.TestUserId,
            GameId = TestConstants.TestGameId,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-6),
            Text = null,
            SignValue = 1, // Positive: makes the post appear in the rated listing
            IsRemoved = false
        });
    }

    // Two active player characters in TestGame, so the tooltips' participants have data.
    private static void SeedCharacters(DmDbContext db)
    {
        if (db.Set<Character>().Any(c => c.CharacterId == TestConstants.TestCharacterId)) return;

        db.Set<Character>().AddRange(
            new Character
            {
                CharacterId = TestConstants.TestCharacterId,
                GameId = TestConstants.TestGameId,
                AuthorId = TestConstants.TestUserId,
                Name = TestConstants.TestCharacterName,
                Status = CharacterStatus.Active,
                IsNpc = false,
                AccessPolicy = CharacterAccessPolicy.NoAccess,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-5),
                IsRemoved = false
            },
            new Character
            {
                CharacterId = TestConstants.SecondCharacterId,
                GameId = TestConstants.TestGameId,
                AuthorId = TestConstants.SecondUserId,
                Name = TestConstants.SecondCharacterName,
                Status = CharacterStatus.Active,
                IsNpc = false,
                AccessPolicy = CharacterAccessPolicy.NoAccess,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-4),
                IsRemoved = false
            });
    }
}
