using System.Net;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbUpload = DM.Infrastructure.Persistence.Entities.Shared.Upload;

using DM.Web.API.IntegrationTests.Helpers;

using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// A file stops existing when the thing it hangs on does.
/// </summary>
/// <remarks>The soft delete and its author are asserted: the stamp starts the grace period the sweeper waits out.</remarks>
public class PostAttachmentLifecycleShould : IntegrationTestBase
{
    public PostAttachmentLifecycleShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static GeneralUser Master => new()
    {
        UserId = TestConstants.TestUserId,
        Username = TestConstants.TestUserLogin,
        Role = UserRole.RegularUser,
    };

    /// <summary>
    /// A game of its own per case: both paths under test end in a soft delete
    /// that other suites would notice.
    /// </summary>
    private async Task<(Guid GameId, Guid PostId, Guid UploadId)> SeedAsync(string publicId)
    {
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var uploadId = Guid.NewGuid();

        await using var db = DatabaseFixture.CreateDbContext();

        db.Set<DbGame>().Add(new DbGame
        {
            GameId = gameId,
            PublicId = publicId,
            MasterId = Master.UserId,
            Title = "Игра для проверки жизненного цикла",
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Info = "Удаление поста и игры забирает вложения.",
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-1),
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CommentsAccessMode = CommentsAccessMode.Public,
            IsRemoved = false,
        });
        db.Set<DbGameAttributeSchema>().Add(GameSheetTestHelper.Sheet(gameId));

        db.Set<DbRoom>().Add(new DbRoom
        {
            RoomId = roomId,
            GameId = gameId,
            RoomNumber = 1,
            Title = "Комната",
            AccessType = RoomAccessType.Open,
            Type = RoomType.Default,
            OrderNumber = 1.0,
            IsRemoved = false,
        });

        db.Set<DbPost>().Add(new DbPost
        {
            PostId = postId,
            RoomId = roomId,
            AuthorId = Master.UserId,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1),
            GameText = "Пост с вложением.",
            PrivateAddresseeSnapshotJson = "{}",
            IsRemoved = false,
        });

        db.Set<DbUpload>().Add(new DbUpload
        {
            UploadId = uploadId,
            UserId = Master.UserId,
            Type = UploadType.PostAttachment,
            Status = UploadStatus.Confirmed,
            TargetPostId = postId,
            Original = true,
            ContentType = "image/png",
            SizeBytes = 10,
            ObjectKey = $"posts/{uploadId:N}.png",
            FilePath = null,
            FileName = "shema.png",
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1),
            ConfirmedUtc = DateTimeOffset.UtcNow.AddHours(-1),
            IsRemoved = false,
        });

        await db.SaveChangesAsync();
        return (gameId, postId, uploadId);
    }

    /// <summary>The row as the sweeper will read it, soft-delete filter ignored.</summary>
    private async Task<DbUpload> StoredUpload(Guid uploadId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        return await db.Uploads.IgnoreQueryFilters().FirstAsync(u => u.UploadId == uploadId);
    }

    [Fact]
    public async Task RetireTheAttachmentsOfADeletedPost()
    {
        var (_, postId, uploadId) = await SeedAsync("gamer");

        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Delete, $"/v1/posts/{postId:D}", Master));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var upload = await StoredUpload(uploadId);
        upload.IsRemoved.Should().BeTrue();
        upload.DeletedUtc.Should().NotBeNull(
            "the stamp is what starts the grace period after which the sweeper drops the object");
        upload.DeletedByUserId.Should().Be(Master.UserId,
            "moderation has to be able to answer who removed a file");
    }

    /// <summary>
    /// And the file stops being served the moment the row is hidden, without
    /// waiting for the sweeper: the endpoint reads live rows only.
    /// </summary>
    [Fact]
    public async Task StopServingTheFileAsSoonAsThePostIsGone()
    {
        var (_, postId, uploadId) = await SeedAsync("games");

        await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Delete, $"/v1/posts/{postId:D}", Master));

        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/uploads/{uploadId:D}/content", Master));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The browser keeps a file a guest reads too, for a guest and a reader signed in alike, and not one whose
    /// mark makes it a matter of the reader: the next account signed in the same browser would get the copy.
    /// </summary>
    [Fact]
    public async Task LetTheBrowserKeepOnlyAFileEveryReaderGets()
    {
        var (_, _, uploadId) = await SeedAsync(Guid.NewGuid().ToString("N")[..10]);
        InMemoryObjectStorage.Seed($"posts/{uploadId:N}.png", [0x89, 0x50, 0x4E, 0x47], "image/png");
        var address = $"/v1/uploads/{uploadId:D}/content";

        foreach (var request in new[]
                 {
                     new HttpRequestMessage(HttpMethod.Get, address),
                     CreateAuthenticatedRequest(HttpMethod.Get, address, Master)
                 })
        {
            var open = await Client.SendAsync(request);
            open.StatusCode.Should().Be(HttpStatusCode.OK);
            open.Headers.CacheControl!.Private.Should().BeTrue();
            open.Headers.CacheControl.NoStore.Should().BeFalse("everybody reads this file");
            open.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(300));
        }

        await using (var db = DatabaseFixture.CreateDbContext())
        {
            await db.Uploads.Where(u => u.UploadId == uploadId)
                .ExecuteUpdateAsync(u => u.SetProperty(row => row.AddresseesJson, "[]"));
        }

        var marked = await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, address, Master));
        marked.StatusCode.Should().Be(HttpStatusCode.OK, "the author reads a file marked for nobody");
        marked.Headers.CacheControl!.NoStore.Should().BeTrue("a marked file is read by some readers and not others");
        marked.Headers.CacheControl.MaxAge.Should().BeNull();
    }

    [Fact]
    public async Task RetireTheAttachmentsOfEveryPostInADeletedGame()
    {
        var (gameId, _, uploadId) = await SeedAsync("gamet");

        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Delete, $"/v1/games/{gameId:D}", Master));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var upload = await StoredUpload(uploadId);
        upload.IsRemoved.Should().BeTrue(
            "a hidden game hides its posts, and with them the only endpoint that would ever serve these bytes");
        upload.DeletedUtc.Should().NotBeNull();
        upload.DeletedByUserId.Should().Be(Master.UserId);
    }
}
