using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CreatePostEntity = DM.Domain.Game.Features.Posts.CreatePostEntity;
using IPostRepository = DM.Domain.Game.Features.Posts.IPostRepository;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <remarks>
/// A roll cannot be produced twice. The rolls travel inside CreatePostEntity into the post's transaction, and the FK
/// DiceRolls.PostId stands behind them.
/// </remarks>
public class PostDiceTransactionShould : IntegrationTestBase
{
    public PostDiceTransactionShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task WriteThePostAndItsRollsTogether()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var entity = Entity(TestConstants.TestRoomId);

        var created = await repository.Create(entity);

        created.Id.Should().Be(entity.PostId);
        (await dbContext.DiceRolls.AsNoTracking().CountAsync(d => d.PostId == entity.PostId))
            .Should().Be(2, "the rolls land in the same transaction as the post");
    }

    [Fact]
    public async Task LeaveNoRollsWhenThePostInsertIsRefused()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        // A room that does not exist: the post insert is refused by its own FK,
        // inside the transaction that also carries the rolls.
        var entity = Entity(roomId: Guid.NewGuid());

        var act = () => repository.Create(entity);

        await act.Should().ThrowAsync<DbUpdateException>(
            "the failure still reaches the caller; what the transaction removes is the remainder");
        (await dbContext.DiceRolls.AsNoTracking().AnyAsync(d => d.PostId == entity.PostId))
            .Should().BeFalse("a refused post leaves no rolls behind");
    }

    private static CreatePostEntity Entity(Guid roomId)
    {
        var postId = Guid.NewGuid();
        return new CreatePostEntity
        {
            PostId = postId,
            RoomId = roomId,
            AuthorId = TestConstants.TestUserId,
            GameText = "Бросаю кости и вхожу в комнату",
            PrivateAddresseeSnapshotJson = "{}",
            CreatedUtc = DateTimeOffset.UtcNow,
            DiceRolls = new List<DiceRoll>
            {
                Roll(postId, "Восприятие"),
                Roll(postId, "Убеждение"),
            }
        };
    }

    private static DiceRoll Roll(Guid postId, string comment) => new()
    {
        Id = Guid.NewGuid(),
        PostId = postId,
        CreatedUtc = DateTimeOffset.UtcNow,
        DiceCount = 1,
        EdgesCount = 20,
        Bonus = 0,
        Comment = comment,
        Results = new[] { new DiceRollResult { Value = 11 } }
    };
}
