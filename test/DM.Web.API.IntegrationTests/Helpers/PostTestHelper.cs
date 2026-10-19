using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;

namespace DM.Web.API.IntegrationTests.Helpers;

/// <summary>
/// Rows the room and search suites put in front of the API, and the cleanup that
/// takes them out again.
/// </summary>
/// <remarks>Written straight to the database: the rules are about what the read path does with existing rows.</remarks>
public static class PostTestHelper
{
    /// <summary>
    /// One post in the seeded room, carrying a [private] block whose frozen
    /// snapshot resolves the addressed character to the second seeded user.
    /// </summary>
    public static async Task<Guid> CreatePrivatePost(
        DatabaseFixture dbFixture, string publicText, string privateText)
    {
        var postId = Guid.NewGuid();

        using var scope = dbFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        dbContext.Set<DbPost>().Add(new DbPost
        {
            PostId = postId,
            RoomId = TestConstants.TestRoomId,
            CharacterId = TestConstants.TestCharacterId,
            AuthorId = TestConstants.TestUserId,
            CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            GameText =
                $"{publicText} [private=\"{TestConstants.SecondCharacterName}\"]{privateText}[/private]",
            MetagameText = null,
            PrivateAddresseeSnapshotJson =
                $"{{\"{TestConstants.SecondCharacterName}\":[{{\"owner\":\"{TestConstants.SecondUserId}\"}}]}}",
            IsRemoved = false
        });

        await dbContext.SaveChangesAsync();
        return postId;
    }

    /// <summary>Takes one post out of the shared database.</summary>
    public static async Task DropPost(DatabaseFixture dbFixture, Guid postId)
    {
        using var scope = dbFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Posts" WHERE "PostId" = {0}""", postId);
    }

    /// <summary>Takes the comments of every topic of a board out.</summary>
    public static async Task DropComments(DatabaseFixture dbFixture, Guid boardId)
    {
        using var scope = dbFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Comments" WHERE "EntityId" IN (SELECT "TopicId" FROM "Topics" WHERE "BoardId" = {0})""",
            boardId);
    }
}
