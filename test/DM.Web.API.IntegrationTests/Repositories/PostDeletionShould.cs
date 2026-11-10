using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Repositories;

public class PostDeletionShould : PostRepositoryTestBase
{
    public PostDeletionShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    // QuantityRating feeds the user rating and the stored IsNewbie column, and nothing recomputes
    // either.
    [Fact]
    public async Task TakeTheAuthorsPostCountDownWithTheRemovedPost()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var context = await AddGameWithRoomAsync(dbContext);
        var characterId = await AddPostByNewCharacterAsync(dbContext, context);
        var postId = await dbContext.Posts.AsNoTracking()
            .Where(p => p.CharacterId == characterId)
            .Select(p => p.PostId)
            .SingleAsync();

        await dbContext.Users.Where(u => u.UserId == context.UserId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.QuantityRating, 7));

        await repository.Delete(postId, context.UserId);

        var author = await dbContext.Users.AsNoTracking().FirstAsync(u => u.UserId == context.UserId);
        author.QuantityRating.Should().Be(6);
        var post = await dbContext.Posts.IgnoreQueryFilters().AsNoTracking()
            .FirstAsync(p => p.PostId == postId);
        post.IsRemoved.Should().BeTrue();
        post.DeletedByUserId.Should().Be(context.UserId);
    }

    [Fact]
    public async Task LeaveTheCountAloneWhenThereIsNoSuchPost()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var context = await AddGameWithRoomAsync(dbContext);

        await dbContext.Users.Where(u => u.UserId == context.UserId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.QuantityRating, 7));

        await repository.Delete(Guid.NewGuid(), context.UserId);

        var author = await dbContext.Users.AsNoTracking().FirstAsync(u => u.UserId == context.UserId);
        author.QuantityRating.Should().Be(7);
    }

    // A scope apiece and a timeout on the pair: the second removal waits on the row lock.
    [Fact]
    public async Task TakeTheAuthorsPostCountDownOnceWhenTwoRemovalsRaceForOnePost()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var context = await AddGameWithRoomAsync(dbContext);
        var characterId = await AddPostByNewCharacterAsync(dbContext, context);
        var postId = await dbContext.Posts.AsNoTracking()
            .Where(p => p.CharacterId == characterId)
            .Select(p => p.PostId)
            .SingleAsync();

        await dbContext.Users.Where(u => u.UserId == context.UserId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.QuantityRating, 7));

        var removals = await Task
            .WhenAll(Remove(postId, context.UserId), Remove(postId, context.UserId))
            .WaitAsync(TimeSpan.FromSeconds(30));

        removals.Count(removed => removed).Should().Be(1,
            "the mark is conditional on the post still being there, so one of the two " +
            "calls is the removal and the other found nothing to remove");

        var author = await dbContext.Users.AsNoTracking().FirstAsync(u => u.UserId == context.UserId);
        author.QuantityRating.Should().Be(6,
            "QuantityRating is a stored column that nothing recomputes, so a second " +
            "decrement for one post is a drift with nothing left to catch it");
    }

    // One removal on a scope of its own, as a request would have.
    private Task<bool> Remove(Guid postId, Guid deletedByUserId) => Task.Run(async () =>
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IPostRepository>()
            .Delete(postId, deletedByUserId);
    });
}
