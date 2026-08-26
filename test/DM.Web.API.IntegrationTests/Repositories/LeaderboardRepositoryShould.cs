using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Statistics;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The boards on the community page are eight grouped aggregates, and grouping is
/// where the reader cannot check the answer.
/// </summary>
/// <remarks>
/// A leaderboard is plausible whatever it says: nobody knows who the top ten
/// should be, so a sum credited to the wrong column produces a page that looks
/// finished. Two properties decide whether the numbers mean anything, and both
/// have already been wrong here: the rating of a post belongs to whoever wrote
/// the post rather than to whoever rated it, and the window is half-open, which
/// is the difference between a week that ends and a week that swallows the next
/// one's first day.
///
/// Run against the database rather than against a mocked set: the whole of the
/// method is one translated GroupBy, and an in-memory provider answers a question
/// PostgreSQL was never asked.
/// </remarks>
public class LeaderboardRepositoryShould : IntegrationTestBase
{
    public LeaderboardRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// The Monday of the week this test owns, far from anything the fixture
    /// seeds. A week of its own per test, because the database is shared and a
    /// board is an aggregate: rows another test left inside the same window would
    /// join the answer and the count would depend on the execution order. The
    /// weeks stand a month apart rather than back to back, so the rows written
    /// just outside a window land in a gap and not in a neighbour's week.
    /// </summary>
    private static DateTimeOffset WeekOf(int index) =>
        new DateTimeOffset(2019, 3, 4, 0, 0, 0, TimeSpan.Zero).AddDays(28 * index);

    [Fact]
    public async Task CreditTheRatingOfAPostToItsAuthorAndNotToTheReviewer()
    {
        var start = WeekOf(0);
        var end = start.AddDays(7);

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICommunityStatsRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var writer = await AddUserAsync(dbContext);
        var otherWriter = await AddUserAsync(dbContext);
        var game = await AddGameWithRoomAsync(dbContext, writer);

        // Two ratings for one author and one for the other, so the order is
        // decided by the sum rather than by the row count.
        await AddReviewAsync(dbContext, game, writer, 1, start.AddDays(1));
        await AddReviewAsync(dbContext, game, writer, 1, start.AddDays(2));
        await AddReviewAsync(dbContext, game, otherWriter, 1, start.AddDays(3));

        var boards = await repository.GetLeaderboards(start, end);

        var byRating = boards.TopPlayersByRating;
        byRating.Should().HaveCount(2, "only the two authors were rated inside this week");
        byRating[0].EntityId.Should().Be(writer, "two points beat one");
        byRating[0].Score.Should().Be(2);
        byRating[1].EntityId.Should().Be(otherWriter);
        byRating[1].Score.Should().Be(1);
        byRating[0].Name.Should().NotBeNullOrEmpty(
            "the board shows a username, which comes from the join to Users");
    }

    [Fact]
    public async Task CountARatingIntoTheWeekItWasGivenInAndNoOther()
    {
        var start = WeekOf(1);
        var end = start.AddDays(7);

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICommunityStatsRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var writer = await AddUserAsync(dbContext);
        var game = await AddGameWithRoomAsync(dbContext, writer);

        // One on each boundary and one inside. The window is half-open, so the
        // row at the closing instant belongs to the next week: counted here it
        // would be counted twice over two consecutive weeks.
        await AddReviewAsync(dbContext, game, writer, 1, start.AddSeconds(-1));
        await AddReviewAsync(dbContext, game, writer, 1, start);
        await AddReviewAsync(dbContext, game, writer, 1, end);

        var boards = await repository.GetLeaderboards(start, end);

        boards.TopPlayersByRating.Should().ContainSingle()
            .Which.Score.Should().Be(1,
                "the row before the week and the row at the closing instant are outside it");
    }

    [Fact]
    public async Task LeaveAWithdrawnRatingOutOfTheBoard()
    {
        var start = WeekOf(2);
        var end = start.AddDays(7);

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICommunityStatsRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var writer = await AddUserAsync(dbContext);
        var game = await AddGameWithRoomAsync(dbContext, writer);

        await AddReviewAsync(dbContext, game, writer, 1, start.AddDays(1));
        await AddReviewAsync(dbContext, game, writer, 1, start.AddDays(2), isRemoved: true);

        var boards = await repository.GetLeaderboards(start, end);

        boards.TopPlayersByRating.Should().ContainSingle()
            .Which.Score.Should().Be(1, "a deleted review is not an opinion anybody holds");
    }

    [Fact]
    public async Task CreditThePostsOfAGameToThatGame()
    {
        var start = WeekOf(3);
        var end = start.AddDays(7);

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICommunityStatsRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var writer = await AddUserAsync(dbContext);
        var game = await AddGameWithRoomAsync(dbContext, writer);
        var otherGame = await AddGameWithRoomAsync(dbContext, writer);

        await AddPostAsync(dbContext, game, writer, start.AddDays(1));
        await AddPostAsync(dbContext, game, writer, start.AddDays(2));
        await AddPostAsync(dbContext, otherGame, writer, start.AddDays(3));

        var boards = await repository.GetLeaderboards(start, end);

        // The post row carries no game: this board reaches it through the room,
        // which is the join a projection loses without failing.
        var byPosts = boards.TopGamesByPosts;
        byPosts.Should().HaveCount(2);
        byPosts[0].EntityId.Should().Be(game.GameId);
        byPosts[0].Score.Should().Be(2);
        byPosts[0].Name.Should().Be(game.Title);
        byPosts[1].EntityId.Should().Be(otherGame.GameId);
        byPosts[1].Score.Should().Be(1);

        boards.TopPlayersByPosts.Should().ContainSingle()
            .Which.Score.Should().Be(3, "all three posts are that player's");
    }

    /// <summary>
    /// The volume board counts the text a reader of the room got, not the
    /// characters the author typed to produce it.
    /// </summary>
    /// <remarks>
    /// It used to count the source, which put two kinds of character into a
    /// public score: the markup, which nobody reads, and the contents of
    /// [private], which the room is not allowed to read. The second one is the
    /// reason this is not merely inaccurate - a player could take the top place
    /// with text that no reader could see and no reader could tell had been
    /// counted.
    ///
    /// The two posts below are built so the answer inverts: the padded one is
    /// four times the plain one as source and a fifth of it as text. On the old
    /// query the padded author heads the board.
    /// </remarks>
    [Fact]
    public async Task CountTheVisibleTextOfAPostAndNotItsSource()
    {
        var start = WeekOf(4);
        var end = start.AddDays(7);

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICommunityStatsRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var plainWriter = await AddUserAsync(dbContext);
        var paddedWriter = await AddUserAsync(dbContext);
        var game = await AddGameWithRoomAsync(dbContext, plainWriter);

        var plainText = new string('я', 100);
        var padded = "[b]" + new string('я', 20) + "[/b]" +
                     "[private=\"" + new string('и', 40) + "\"]" + new string('я', 300) + "[/private]";

        await AddPostAsync(dbContext, game, plainWriter, start.AddDays(1), plainText);
        await AddPostAsync(dbContext, game, paddedWriter, start.AddDays(2), padded);

        var boards = await repository.GetLeaderboards(start, end);

        var byVolume = boards.TopPlayersByVolume;
        byVolume.Should().HaveCount(2);
        byVolume[0].EntityId.Should().Be(plainWriter,
            "a hundred characters a reader can see beat twenty a reader can see");
        byVolume[0].Score.Should().Be(plainText.Length);
        byVolume[1].EntityId.Should().Be(paddedWriter);
        byVolume[1].Score.Should().Be(20,
            "neither the tags nor the hidden block are text anybody was shown");
    }

    private sealed record GameContext(Guid GameId, Guid RoomId, Guid CharacterId, string Title);

    /// <summary>
    /// A game, a room and a character of its own per call: the fixture's database
    /// is shared and seeded, so fixed identifiers would collide.
    /// </summary>
    private static async Task<GameContext> AddGameWithRoomAsync(DmDbContext dbContext, Guid masterId)
    {
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var title = "Игра " + gameId.ToString("N")[..6];

        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            // PublicId is NOT NULL and uniquely indexed.
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Title = title,
            MasterId = masterId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });
        dbContext.Rooms.Add(new DbRoom
        {
            RoomId = roomId,
            GameId = gameId,
            RoomNumber = 1,
            Title = "Room",
            AccessType = RoomAccessType.Open,
            OrderNumber = 1,
        });
        dbContext.Characters.Add(new DbCharacter
        {
            CharacterId = characterId,
            GameId = gameId,
            AuthorId = masterId,
            Status = CharacterStatus.Active,
            Name = "Char " + characterId.ToString("N")[..6],
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        return new GameContext(gameId, roomId, characterId, title);
    }

    private static async Task<Guid> AddPostAsync(
        DmDbContext dbContext,
        GameContext game,
        Guid authorId,
        DateTimeOffset createdUtc,
        string gameText = "text")
    {
        var postId = Guid.NewGuid();
        dbContext.Posts.Add(new DbPost
        {
            PostId = postId,
            RoomId = game.RoomId,
            CharacterId = game.CharacterId,
            AuthorId = authorId,
            GameText = gameText,
            CreatedUtc = createdUtc,
        });

        await dbContext.SaveChangesAsync();
        return postId;
    }

    /// <summary>
    /// One rating of a post by <paramref name="postAuthorId"/>, written by
    /// somebody else: the pair (AuthorId, PostId) is uniquely indexed, and the
    /// reviewer is exactly the person the board must not credit.
    /// </summary>
    private static async Task AddReviewAsync(
        DmDbContext dbContext,
        GameContext game,
        Guid postAuthorId,
        short signValue,
        DateTimeOffset createdUtc,
        bool isRemoved = false)
    {
        var postId = await AddPostAsync(dbContext, game, postAuthorId, createdUtc);

        dbContext.PostReviews.Add(new DbPostReview
        {
            PostReviewId = Guid.NewGuid(),
            PostId = postId,
            AuthorId = await AddUserAsync(dbContext),
            PostAuthorId = postAuthorId,
            GameId = game.GameId,
            SignValue = signValue,
            Text = "Отличный отыгрыш!",
            CreatedUtc = createdUtc,
            IsRemoved = isRemoved,
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> AddUserAsync(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();

        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id is
            // truncated rather than used whole.
            Username = $"brd{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });

        await dbContext.SaveChangesAsync();
        return userId;
    }
}
