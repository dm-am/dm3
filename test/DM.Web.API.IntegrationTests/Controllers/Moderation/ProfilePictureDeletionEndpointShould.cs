using System.Net;
using DM.Domain.Core.Dto;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbUpload = DM.Infrastructure.Persistence.Entities.Shared.Upload;
using DM.Web.API.IntegrationTests.Helpers;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

public class ProfilePictureDeletionEndpointShould : WarningControllerTestBase
{
    public ProfilePictureDeletionEndpointShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private Task<HttpResponseMessage> DeleteProfilePicture(Guid warningId, GeneralUser user) =>
        Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Delete, $"/v1/moderation/warnings/{warningId}/profile-picture", user));

    private async Task<DbUpload> StoredUpload(Guid uploadId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        return await db.Uploads.IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.UploadId == uploadId);
    }

    // Whatever its owner did to the avatar: the other routes read through the soft-delete filter.
    [Theory]
    [InlineData("still the avatar")]
    [InlineData("replaced by its owner")]
    [InlineData("deleted by its owner")]
    public async Task TakeThePinnedPictureOutOfBothLogsForAModerator(string story)
    {
        var warningId = LogId(0x75);
        var uploadId = Guid.NewGuid();
        var avatar = story switch
        {
            "still the avatar" => Avatar(uploadId, TestConstants.SecondUserId, removed: false),
            "replaced by its owner" => Avatar(uploadId, TestConstants.SecondUserId),
            _ => Avatar(uploadId, TestConstants.SecondUserId, deletedBy: TestConstants.SecondUserId)
        };
        await WithPinnedPicture(avatar,
            OnProfile(warningId, TestConstants.SecondUserId, "Статус", "[b]О себе[/b]", uploadId), async () =>
        {
            var response = await DeleteProfilePicture(warningId, CustomWebApplicationFactory.CreateModeratorUser());
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.NoContent, "body was: {0}", body);

            var stored = await StoredUpload(uploadId);
            stored.IsRemoved.Should().BeTrue("the picture is {0} and moderation deleted it", story);
            stored.DeletedByUserId.Should().Be(TestConstants.ModeratorUserId,
                "the deletion is the moderator's, which is what lets the sweeper take the pinned file");
            stored.DeletedUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5),
                "the grace period starts when moderation deleted the picture, not when the owner replaced it");

            using var open = await ReadDocument(
                await Client.GetAsync($"/v1/warnings?username={TestConstants.SecondUserLogin}"));
            using var moderation = await ReadDocument(await Client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Get, $"/v1/moderation/warnings?username={TestConstants.SecondUserLogin}",
                CustomWebApplicationFactory.CreateModeratorUser())));
            foreach (var (name, document) in new[] { ("open log", open), ("moderation", moderation) })
            {
                var profile = RowOf(document, warningId).GetProperty("profile");
                profile.TryGetProperty("picture", out _).Should().BeFalse("{0} no longer shows the picture", name);
                profile.GetProperty("status").GetString().Should().Be("Статус", "{0} keeps the status", name);
                profile.GetProperty("info").GetString().Should().Contain("<strong>О себе</strong>",
                    "{0} keeps О себе", name);
            }
        });
    }

    // One conditional statement (DATA_STORAGE.md, "Атомарные встречные записи").
    [Fact]
    public async Task AnswerASecondDeletionWithSuccessAndKeepTheFirstMark()
    {
        var warningId = LogId(0x76);
        var uploadId = Guid.NewGuid();
        await WithPinnedPicture(Avatar(uploadId, TestConstants.SecondUserId),
            OnProfile(warningId, TestConstants.SecondUserId, "Статус", null, uploadId), async () =>
        {
            (await DeleteProfilePicture(warningId, CustomWebApplicationFactory.CreateModeratorUser()))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
            var first = await StoredUpload(uploadId);

            (await DeleteProfilePicture(warningId, CustomWebApplicationFactory.CreateSeniorModeratorUser()))
                .StatusCode.Should().Be(HttpStatusCode.NoContent, "another moderator pressed it too");
            (await DeleteProfilePicture(warningId, CustomWebApplicationFactory.CreateModeratorUser()))
                .StatusCode.Should().Be(HttpStatusCode.NoContent, "the same moderator pressed it again");

            var after = await StoredUpload(uploadId);
            after.IsRemoved.Should().BeTrue();
            after.DeletedByUserId.Should().Be(TestConstants.ModeratorUserId, "the first deletion stays signed");
            after.DeletedUtc.Should().Be(first.DeletedUtc, "the grace period is not restarted");
        });
    }

    [Fact]
    public async Task RefuseTheDeletionOfAPinnedPictureToAReader()
    {
        var warningId = LogId(0x77);
        var uploadId = Guid.NewGuid();
        await WithPinnedPicture(Avatar(uploadId, TestConstants.SecondUserId),
            OnProfile(warningId, TestConstants.SecondUserId, "Статус", null, uploadId), async () =>
        {
            var asReader = await DeleteProfilePicture(warningId, CustomWebApplicationFactory.CreateTestUser());
            asReader.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var asGuest = await Client.DeleteAsync($"/v1/moderation/warnings/{warningId}/profile-picture");
            asGuest.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var stored = await StoredUpload(uploadId);
            stored.DeletedByUserId.Should().BeNull("the refusal must leave the replaced avatar as its owner left it");
            using var open = await ReadDocument(
                await Client.GetAsync($"/v1/warnings?username={TestConstants.SecondUserLogin}"));
            RowOf(open, warningId).GetProperty("profile").TryGetProperty("picture", out _)
                .Should().BeTrue("the picture stays in the log");
        });
    }

    // A party does not rule on his own case, as with tickets.
    [Fact]
    public async Task RefuseAModeratorThePictureOfAWarningIssuedToHimself()
    {
        var warningId = LogId(0x7a);
        var uploadId = Guid.NewGuid();
        var avatar = Avatar(uploadId, TestConstants.ModeratorUserId, removed: false);
        await SeedUpload(avatar);
        Guid? avatarBefore;
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(u => u.UserId == TestConstants.ModeratorUserId);
            avatarBefore = user.AvatarUploadId;
            user.AvatarUploadId = uploadId;
            await db.SaveChangesAsync();
        }

        var warning = OnProfile(warningId, TestConstants.ModeratorUserId, "Статус", null, uploadId);
        warning.AuthorId = TestConstants.SeniorModeratorUserId;
        await Seed(warning);

        try
        {
            var response = await DeleteProfilePicture(warningId, CustomWebApplicationFactory.CreateModeratorUser());
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body was: {0}", body);
            body.Should().Contain("Убрать картинку из лога о вас может только другой модератор");

            var stored = await StoredUpload(uploadId);
            stored.IsRemoved.Should().BeFalse("the refusal marks nothing");
            stored.DeletedByUserId.Should().BeNull();
            stored.DeletedUtc.Should().BeNull();

            using var profile = await ReadDocument(
                await Client.GetAsync($"/v1/users/{TestConstants.ModeratorUserLogin}"));
            profile.RootElement.GetProperty("resource").GetProperty("picture").GetProperty("originalUrl").GetString()
                .Should().Be(avatar.FilePath, "his avatar stays in his profile");
            using var open = await ReadDocument(
                await Client.GetAsync($"/v1/warnings?username={TestConstants.ModeratorUserLogin}"));
            RowOf(open, warningId).GetProperty("profile").TryGetProperty("picture", out _)
                .Should().BeTrue("the picture stays in the log");
        }
        finally
        {
            await Unseed(warningId);
            await using (var db = DatabaseFixture.CreateDbContext())
            {
                var user = await db.Users.SingleAsync(u => u.UserId == TestConstants.ModeratorUserId);
                user.AvatarUploadId = avatarBefore;
                await db.SaveChangesAsync();
            }

            await UnseedUpload(uploadId);
        }
    }

    [Fact]
    public async Task AnswerAWarningWithoutAPictureWithSuccessAndAnUnknownOneWithNotFound()
    {
        var warningId = LogId(0x78);
        await Seed(OnProfile(warningId, TestConstants.SecondUserId, "Статус", null, null));

        try
        {
            (await DeleteProfilePicture(warningId, CustomWebApplicationFactory.CreateModeratorUser()))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await DeleteProfilePicture(LogId(0x79), CustomWebApplicationFactory.CreateModeratorUser()))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await Unseed(warningId);
        }
    }
}
