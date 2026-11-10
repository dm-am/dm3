using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence.Repositories.Game;
using AwesomeAssertions;
using Xunit;
using DbDiceRoll = DM.Infrastructure.Persistence.Entities.Game.Posts.DiceRoll;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbRollResult = DM.Infrastructure.Persistence.Entities.Game.Posts.RollResult;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Game;

// Left at their defaults they hide every roll from all but the leads and the author.
public class DiceRollReadShould
{
    private static readonly DateTimeOffset Created = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid AuthorId = Guid.Parse("5d2f1e33-0000-4000-8000-000000000001");

    private static readonly Guid ShowingPostId = Guid.Parse("5d2f1e33-0000-4000-8000-000000000011");
    private static readonly Guid HidingPostId = Guid.Parse("5d2f1e33-0000-4000-8000-000000000012");

    private static DmDbContext Seeded()
    {
        var context = InMemoryStore.Open();
        context.Users.Add(InMemoryStore.User(AuthorId, "author"));
        AddPost(context, "aaaaa", ShowingPostId, hidesResults: false, showsResults: true);
        AddPost(context, "bbbbb", HidingPostId, hidesResults: true, showsResults: false);
        context.SaveChanges();
        return context;
    }

    private static void AddPost(DmDbContext context, string publicId, Guid postId, bool hidesResults, bool showsResults)
    {
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        context.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = publicId,
            MasterId = AuthorId,
            Title = "Игра",
            CreatedUtc = Created,
            HideDiceResult = hidesResults,
        });
        context.Set<DbRoom>().Add(new DbRoom
        {
            RoomId = roomId,
            GameId = gameId,
            Title = "Комната",
            ViewDiceResults = showsResults,
            DiceEnabled = true,
        });
        context.Set<DbPost>().Add(new DbPost
        {
            PostId = postId,
            RoomId = roomId,
            AuthorId = AuthorId,
            GameText = "Бросаю",
            CreatedUtc = Created,
        });
        context.Set<DbDiceRoll>().Add(new DbDiceRoll
        {
            DiceRollId = Guid.NewGuid(),
            PostId = postId,
            CreatedUtc = Created,
            DiceCount = 1,
            EdgesCount = 20,
            Comment = "",
            Result = [new DbRollResult { Value = 17 }],
        });
    }

    [Fact]
    public async Task ReadTheSettingsOfTheRollsOwnRoomAndGameForOnePost()
    {
        using var context = Seeded();
        var repository = new DiceRollRepository(context);

        var showing = (await repository.GetByPostIdAsync(ShowingPostId)).Single();
        var hiding = (await repository.GetByPostIdAsync(HidingPostId)).Single();

        showing.RoomViewDiceResults.Should().BeTrue();
        showing.GameHideDiceResult.Should().BeFalse();
        hiding.RoomViewDiceResults.Should().BeFalse();
        hiding.GameHideDiceResult.Should().BeTrue();
    }

    [Fact]
    public async Task ReadTheSettingsOfTheRollsOwnRoomAndGameForAPageOfPosts()
    {
        using var context = Seeded();
        var repository = new DiceRollRepository(context);

        var rolls = await repository.GetByPostIdsAsync([ShowingPostId, HidingPostId]);

        var showing = rolls[ShowingPostId].Single();
        var hiding = rolls[HidingPostId].Single();
        showing.RoomViewDiceResults.Should().BeTrue();
        showing.GameHideDiceResult.Should().BeFalse();
        hiding.RoomViewDiceResults.Should().BeFalse();
        hiding.GameHideDiceResult.Should().BeTrue();
    }
}
