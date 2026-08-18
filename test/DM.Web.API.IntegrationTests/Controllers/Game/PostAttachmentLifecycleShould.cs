using System.Net;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbUpload = DM.Infrastructure.Persistence.Entities.Shared.Upload;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// A file stops existing when the thing it hangs on does.
/// </summary>
/// <remarks>
/// Before this, nothing ever removed an attachment. The row stayed live when the
/// post was deleted and when the game was, the sweeper walks soft-deleted upload
/// rows and so never saw it, and the object sat in the bucket for the life of the
/// bucket. What is asserted here is the soft delete and its author — that stamp
/// is what starts the grace period the existing sweeper waits out, so getting it
/// written is the whole of "the file goes too".
/// </remarks>
public class PostAttachmentLifecycleShould : IntegrationTestBase
{
    public PostAttachmentLifecycleShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static GeneralUser Master => new()
    {
        UserId = TestConstants.TestUserId,
        Username = TestConstants.TestUserUsername,
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
