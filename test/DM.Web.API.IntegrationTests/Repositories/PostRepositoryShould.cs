using DM.Domain.Core.Content;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CreatePostEntity = DM.Domain.Game.Features.Posts.CreatePostEntity;
using UpdatePostEntity = DM.Domain.Game.Features.Posts.UpdatePostEntity;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The page of a room and the writes of a post: the order a room is paged in,
/// the addressee snapshot both writes store, and the rating every read carries.
/// </summary>
public class PostRepositoryShould : PostRepositoryTestBase
{
    public PostRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Posts written in one instant are spread across the pages, not repeated on them.
    /// </summary>
    /// <remarks>
    /// A timestamp is not unique (the DM2 import writes rooms under one), so the order ends on the identifier, by the
    /// index (RoomId, CreatedUtc, PostId), as comments and messages do.
    /// </remarks>
    [Fact]
    public async Task PageARoomWhosePostsShareOneTimestamp()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        var oneInstant = DateTimeOffset.UtcNow;
        var first = await AddRatedPostAsync(
            dbContext, context, positiveReviews: 0, postCreatedUtc: oneInstant);
        var second = await AddRatedPostAsync(
            dbContext, context, positiveReviews: 0, postCreatedUtc: oneInstant);
        var third = await AddRatedPostAsync(
            dbContext, context, positiveReviews: 0, postCreatedUtc: oneInstant);
        var fourth = await AddRatedPostAsync(
            dbContext, context, positiveReviews: 0, postCreatedUtc: oneInstant);

        // PostgreSQL compares uuid by its bytes, which for the canonical form is the order
        // of its hex digits.
        var expected = new[] { first, second, third, fourth }
            .OrderBy(id => id.ToString("N"), StringComparer.Ordinal)
            .ToArray();

        var firstPage = (await repository.Get(
            context.RoomId,
            new RoomPostsQuery(),
            new PagingData(new PagingQuery { Skip = 0, Take = 2 }, 2, 4),
            context.UserId)).Select(p => p.Id).ToArray();
        var secondPage = (await repository.Get(
            context.RoomId,
            new RoomPostsQuery(),
            new PagingData(new PagingQuery { Skip = 2, Take = 2 }, 2, 4),
            context.UserId)).Select(p => p.Id).ToArray();

        firstPage.Concat(secondPage).Should().Equal(expected,
            "one order runs across both pages, and the identifier is what settles it");
    }

    /// <summary>
    /// The addressee snapshot has to reach its column and come back from it.
    /// </summary>
    /// <remarks>A gate on the write itself, on both paths that perform it.</remarks>
    [Fact]
    public async Task StoreThePrivateAddresseeSnapshotOnBothWritePaths()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        var annaOwner = Guid.NewGuid();
        var borisOwner = Guid.NewGuid();

        var created = await repository.Create(new CreatePostEntity
        {
            PostId = Guid.NewGuid(),
            RoomId = context.RoomId,
            AuthorId = context.UserId,
            GameText = "[private=\"Анна\"]секрет[/private]",
            PrivateAddresseeSnapshotJson = PrivateAddresseeSnapshot.Build(
                "[private=\"Анна\"]секрет[/private]",
                new[] { new PrivateAddressee(Guid.NewGuid(), "Анна", annaOwner) }),
            CreatedUtc = DateTimeOffset.UtcNow,
        });

        PrivateAddresseeSnapshot.Read(await ReadSnapshotAsync(dbContext, created.Id)).OwnerUserIdsByAttribute
            .Should().ContainKey("Анна")
            .WhoseValue.Should().BeEquivalentTo(new[] { annaOwner });

        await repository.Update(new UpdatePostEntity
        {
            PostId = created.Id,
            PostEditId = Guid.NewGuid(),
            EditorUserId = context.UserId,
            EditedUtc = DateTimeOffset.UtcNow,
            GameText = "[private=\"Борис\"]секрет[/private]",
            PrivateAddresseeSnapshotJson = PrivateAddresseeSnapshot.Build(
                "[private=\"Борис\"]секрет[/private]",
                new[] { new PrivateAddressee(Guid.NewGuid(), "Борис", borisOwner) }),
        });

        var afterEdit = PrivateAddresseeSnapshot.Read(await ReadSnapshotAsync(dbContext, created.Id)).OwnerUserIdsByAttribute;
        afterEdit.Should().ContainKey("Борис").WhoseValue.Should().BeEquivalentTo(new[] { borisOwner });
        afterEdit.Should().NotContainKey("Анна");
    }

    /// <summary>
    /// An edit stays in the history of the post, so "Отредактировано" under it
    /// survives the next read; a save that changes nothing writes no line.
    /// </summary>
    [Fact]
    public async Task KeepTheEditInTheHistoryOfThePost()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        var postId = await AddRatedPostAsync(dbContext, context, positiveReviews: 0);
        var stored = await dbContext.Posts.AsNoTracking().SingleAsync(p => p.PostId == postId);
        var editedUtc = DateTimeOffset.UtcNow.AddMinutes(-1);

        UpdatePostEntity Save(string gameText) => new()
        {
            PostId = postId,
            PostEditId = Guid.NewGuid(),
            EditorUserId = context.UserId,
            EditedUtc = editedUtc,
            GameText = gameText,
            MetagameText = stored.MetagameText,
            PrivateAddresseeSnapshotJson = stored.PrivateAddresseeSnapshotJson
        };

        await repository.Update(Save(stored.GameText));
        (await repository.Get(postId, context.UserId))!.Edits.Should().BeEmpty(
            "the same text saved again is no edit");

        await repository.Update(Save(stored.GameText + " и еще строка"));

        // Read again in a scope of its own, as the next page load reads it.
        using var reading = DatabaseFixture.Factory.Services.CreateScope();
        var post = await reading.ServiceProvider.GetRequiredService<IPostRepository>().Get(postId, context.UserId);
        post!.Edits.Should().ContainSingle()
            .Which.ModifiedUtc.Should().BeCloseTo(editedUtc, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task CarryTheRatingAndTheReviewCountOfAPostReadThroughItsRoom()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        var postId = await AddRatedPostAsync(
            dbContext, context, positiveReviews: 3, negativeReviews: 1, removedReviews: 1);

        // A guest, because that is the reader the room page failed for.
        var posts = await repository.Get(
            context.RoomId,
            new RoomPostsQuery(),
            new PagingData(new PagingQuery { Skip = 0, Take = 10 }, 10, 1),
            Guid.Empty);

        var post = posts.Single(x => x.Id == postId);
        // The room page, where reviews are written, carries the rating and the count as the rated path does.
        post.Rating.Should().Be(2, "the rating is the sum of the signs: +1 +1 +1 -1");
        post.ReviewCount.Should().Be(4, "the removed review counts for neither");
    }

    [Fact]
    public async Task CarryTheRatingAndTheReviewCountOfASinglePostRead()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        var postId = await AddRatedPostAsync(
            dbContext, context, positiveReviews: 3, negativeReviews: 1, removedReviews: 1);

        // The point read goes through the same map, and answers the post a
        // permalink names.
        var post = await repository.Get(postId, Guid.Empty);

        post.Should().NotBeNull();
        post!.Rating.Should().Be(2);
        post.ReviewCount.Should().Be(4);
    }

    /// <summary>The stored snapshot of a post, read past the change tracker.</summary>
    private static Task<string> ReadSnapshotAsync(DmDbContext dbContext, Guid postId) =>
        dbContext.Posts
            .AsNoTracking()
            .Where(p => p.PostId == postId)
            .Select(p => p.PrivateAddresseeSnapshotJson)
            .SingleAsync();
}
