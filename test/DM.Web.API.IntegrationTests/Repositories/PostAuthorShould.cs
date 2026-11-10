using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbGameAssistant = DM.Infrastructure.Persistence.Entities.Game.Links.GameAssistant;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

public class PostAuthorShould : PostRepositoryTestBase
{
    public PostAuthorShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <remarks>
    /// The schema refuses a second live portrait, so the insert through the endpoint's repository retires the row it
    /// replaces in the same transaction.
    /// </remarks>
    [Fact]
    public async Task ShowThePortraitThatReplacedTheOneBeforeIt()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var uploads = scope.ServiceProvider.GetRequiredService<IUploadRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        var characterId = await AddPostByNewCharacterAsync(dbContext, context);

        var older = await AddCharacterPortraitAsync(
            uploads, context.UserId, characterId, DateTimeOffset.UtcNow.AddHours(-1));
        var newer = await AddCharacterPortraitAsync(
            uploads, context.UserId, characterId, DateTimeOffset.UtcNow);

        var posts = (await repository.Get(
            context.RoomId,
            new RoomPostsQuery(),
            new PagingData(new PagingQuery { Skip = 0, Take = 10 }, 10, 1),
            context.UserId)).ToList();

        posts.Should().ContainSingle();
        posts[0].Character.Picture.SourceObjectKey.Should().Be(newer,
            "the current portrait is the live upload pointing at the character");
        posts[0].Character.Picture.SourceObjectKey.Should().NotBe(older);
    }

    /// <remarks>
    /// Npgsql cannot correlate a collection subquery concatenated onto an in-memory array. The assistant makes this a
    /// gate: with the master alone both shapes agree.
    /// </remarks>
    [Fact]
    public async Task ProjectTheGameLeadsOfEveryPostInARoom()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        await AddPostByNewCharacterAsync(dbContext, context);
        var assistantId = await AddAssistantAsync(dbContext, context.GameId);

        var posts = (await repository.Get(
            context.RoomId,
            new RoomPostsQuery(),
            new PagingData(new PagingQuery { Skip = 0, Take = 10 }, 10, 1),
            context.UserId)).ToList();

        posts.Should().ContainSingle();
        posts[0].GameLeadUserIds.Should().BeEquivalentTo(new[] { context.UserId, assistantId },
            "the leads are the master and the assistants, and mentors are not leads");

        // The single-post path shares the projection, so it shares the failure.
        var single = await repository.Get(posts[0].Id, context.UserId);
        single.Should().NotBeNull();
        single!.GameLeadUserIds.Should().BeEquivalentTo(new[] { context.UserId, assistantId });
    }

    // An assistant on the game, so the lead list has two entries.
    private static async Task<Guid> AddAssistantAsync(DmDbContext dbContext, Guid gameId)
    {
        var assistantId = Guid.NewGuid();

        dbContext.Users.Add(new DbUser
        {
            UserId = assistantId,
            Username = $"ast{assistantId:N}"[..20],
            Email = $"{assistantId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        dbContext.GameAssistants.Add(new DbGameAssistant
        {
            GameAssistantId = Guid.NewGuid(),
            GameId = gameId,
            UserId = assistantId,
            JoinedUtc = DateTimeOffset.UtcNow,
        });

        await dbContext.SaveChangesAsync();
        return assistantId;
    }

    /// <summary>
    /// A character portrait written as the upload endpoint writes it; returns its object key.
    /// </summary>
    private static async Task<string> AddCharacterPortraitAsync(
        IUploadRepository uploads, Guid userId, Guid characterId, DateTimeOffset createdUtc)
    {
        var objectKey = $"characters/{Guid.NewGuid():N}.png";

        await uploads.AddAsync(new NewUpload
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TargetId = characterId,
            Type = UploadType.CharacterAvatar,
            Status = UploadStatus.Confirmed,
            FileName = "portrait.png",
            ContentType = "image/png",
            SizeBytes = 1024,
            ObjectKey = objectKey,
            Original = true,
            Url = $"https://cdn.example/{objectKey}",
            CreatedUtc = createdUtc,
            ConfirmedUtc = createdUtc,
        });

        return objectKey;
    }
}
