using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DM.Web.API.IntegrationTests.Helpers;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

public class ProfileWarningShould : WarningControllerTestBase
{
    public ProfileWarningShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    // The object is the warned user, so the row names no place and no address; the reason is
    // rendered.
    [Fact]
    public async Task ShowTheProfileAWarningWasIssuedForInBothLogs()
    {
        var warningId = LogId(0x71);
        var uploadId = Guid.NewGuid();
        var avatar = Avatar(uploadId, TestConstants.SecondUserId);
        await WithPinnedPicture(avatar,
            OnProfile(warningId, TestConstants.SecondUserId,
                "Статус на момент выдачи", "[b]О себе[/b] на момент выдачи", uploadId,
                text: "[b]Неприемлемый[/b] аватар"), async () =>
        {
            using var open = await ReadDocument(
                await Client.GetAsync($"/v1/warnings?username={TestConstants.SecondUserLogin}"));
            using var moderation = await ReadDocument(await Client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Get, $"/v1/moderation/warnings?username={TestConstants.SecondUserLogin}",
                CustomWebApplicationFactory.CreateModeratorUser())));

            var rows = new[] { ("open log", RowOf(open, warningId)), ("moderation", RowOf(moderation, warningId)) };
            foreach (var (name, row) in rows)
            {
                row.GetProperty("entityType").GetString().Should().Be("Profile", name);
                row.GetProperty("reason").GetString().Should().Contain("<strong>Неприемлемый</strong>")
                    .And.NotContain("[b]", "{0} shows the reason, not its source", name);
                row.TryGetProperty("evidence", out _).Should().BeFalse(
                    "{0}: the words of a profile are given with the profile, not as a text", name);
                row.TryGetProperty("entityUrl", out _).Should().BeFalse("{0}: a profile has no place to link", name);
                row.TryGetProperty("placeKind", out _).Should().BeFalse(name);
                row.TryGetProperty("entityCreatedUtc", out _).Should().BeFalse(name);

                var profile = row.GetProperty("profile");
                profile.GetProperty("status").GetString().Should().Be("Статус на момент выдачи", name);
                profile.GetProperty("info").GetString().Should().Contain("<strong>О себе</strong>")
                    .And.NotContain("[b]", "{0} renders О себе as the profile does", name);
                var picture = profile.GetProperty("picture");
                picture.GetProperty("originalUrl").GetString().Should().Be(avatar.FilePath,
                    "{0} keeps the avatar its owner replaced after the warning", name);
                picture.GetProperty("originalWidth").GetInt32().Should().Be(731, name);
                picture.GetProperty("originalHeight").GetInt32().Should().Be(793, name);
            }

            RowOf(moderation, warningId).GetProperty("entityId").GetGuid().Should().Be(TestConstants.SecondUserId);
        });
    }

    // The pinned avatar is kept from its owner, not from moderation.
    [Theory]
    [InlineData("still the avatar", true)]
    [InlineData("replaced by its owner", true)]
    [InlineData("deleted by its owner", true)]
    [InlineData("deleted by moderation", false)]
    public async Task ShowThePinnedPictureUntilModerationDeletesIt(string story, bool shown)
    {
        var warningId = LogId(0x72);
        var uploadId = Guid.NewGuid();
        var avatar = story switch
        {
            "still the avatar" => Avatar(uploadId, TestConstants.SecondUserId, removed: false),
            "replaced by its owner" => Avatar(uploadId, TestConstants.SecondUserId),
            "deleted by its owner" => Avatar(uploadId, TestConstants.SecondUserId,
                deletedBy: TestConstants.SecondUserId),
            _ => Avatar(uploadId, TestConstants.SecondUserId, deletedBy: TestConstants.ModeratorUserId)
        };
        await WithPinnedPicture(avatar,
            OnProfile(warningId, TestConstants.SecondUserId, "Статус", null, uploadId), async () =>
        {
            using var document = await ReadDocument(
                await Client.GetAsync($"/v1/warnings?username={TestConstants.SecondUserLogin}"));
            var profile = RowOf(document, warningId).GetProperty("profile");

            profile.TryGetProperty("picture", out _).Should().Be(shown, "the avatar is {0}", story);
            profile.GetProperty("status").GetString().Should().Be("Статус",
                "the status stays whatever became of the picture");
            profile.TryGetProperty("info", out _).Should().BeFalse("a profile without О себе gives none");
        });
    }

    // Today the read loses the row (RemovedAuthorRowsShould); the projection hides the profile on
    // its own too.
    [Fact]
    public async Task GiveNoProfileOfARemovedAccount()
    {
        var warningId = LogId(0x73);
        var uploadId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        var status = $"Статус удаленного {removedId:N}";
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            db.Users.Add(new DbUser
            {
                UserId = removedId,
                Username = $"gone{removedId:N}"[..20],
                Email = $"{removedId:N}@removed.example.com",
                PasswordHash = "hash",
                Salt = "salt",
                CreatedUtc = DateTimeOffset.UtcNow.AddYears(-1),
                IsRemoved = true
            });
            await db.SaveChangesAsync();
        }

        await SeedUpload(Avatar(uploadId, removedId));
        await Seed(OnProfile(warningId, removedId, status, "О себе удаленного", uploadId));

        try
        {
            var open = await Client.GetAsync("/v1/warnings?take=100");
            var moderation = await Client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Get, "/v1/moderation/warnings?take=100", CustomWebApplicationFactory.CreateModeratorUser()));

            foreach (var response in new[] { open, moderation })
            {
                var body = await response.Content.ReadAsStringAsync();
                response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);
                body.Should().NotContain(status).And.NotContain("О себе удаленного")
                    .And.NotContain(uploadId.ToString("N"), "nothing of a removed account is shown");
            }
        }
        finally
        {
            await Unseed(warningId);
            await UnseedUpload(uploadId);
            await using var db = DatabaseFixture.CreateDbContext();
            await db.Users.IgnoreQueryFilters().Where(u => u.UserId == removedId).ExecuteDeleteAsync();
        }
    }

    // The sweeper deletes the row once the grace period is over.
    [Fact]
    public async Task ClearThePinWhenTheUploadIsGone()
    {
        var warningId = LogId(0x74);
        var uploadId = Guid.NewGuid();
        await WithPinnedPicture(Avatar(uploadId, TestConstants.SecondUserId, deletedBy: TestConstants.ModeratorUserId),
            OnProfile(warningId, TestConstants.SecondUserId, "Статус", null, uploadId), async () =>
        {
            await UnseedUpload(uploadId);

            await using var db = DatabaseFixture.CreateDbContext();
            var stored = await db.Warnings.SingleAsync(w => w.WarningId == warningId);
            stored.ProfileAvatarUploadId.Should().BeNull();
            stored.ProfileStatusSnapshot.Should().Be("Статус", "the warning outlives its picture");
        });
    }

    // A warning on a profile is on the warned user: the named identifier is not read, the profile
    // copied at issue.
    [Fact]
    public async Task CopyTheProfileOfTheWarnedUserWhicheverIdentifierTheRequestNamed()
    {
        var uploadId = Guid.NewGuid();
        var avatar = Avatar(uploadId, TestConstants.SecondUserId, removed: false);
        await SeedUpload(avatar);
        string? statusBefore;
        string? infoBefore;
        Guid? avatarBefore;
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(u => u.UserId == TestConstants.SecondUserId);
            (statusBefore, infoBefore, avatarBefore) = (user.Status, user.Info, user.AvatarUploadId);
            user.Status = "Статус перед выдачей";
            user.Info = "[i]О себе[/i] перед выдачей";
            user.AvatarUploadId = uploadId;
            await db.SaveChangesAsync();
        }

        Guid? warningId = null;
        try
        {
            var request = CreateAuthenticatedRequest(
                HttpMethod.Post, "/v1/moderation/warnings", CustomWebApplicationFactory.CreateModeratorUser());
            request.Content = JsonContent.Create(new
            {
                username = TestConstants.SecondUserLogin,
                entityType = "Profile",
                entityId = TestConstants.TestTopicId,
                points = 0,
                reason = "Неприемлемый статус"
            });

            var response = await Client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.Created, "body was: {0}", body);

            using var document = JsonDocument.Parse(body);
            var resource = document.RootElement.GetProperty("resource");
            warningId = resource.GetProperty("id").GetGuid();
            resource.GetProperty("entityType").GetString().Should().Be("Profile");
            resource.GetProperty("entityId").GetGuid().Should().Be(TestConstants.SecondUserId,
                "the object of a warning on a profile is the warned user, whatever the request named");
            var profile = resource.GetProperty("profile");
            profile.GetProperty("status").GetString().Should().Be("Статус перед выдачей");
            profile.GetProperty("info").GetString().Should().Contain("<em>О себе</em>");
            profile.GetProperty("picture").GetProperty("originalUrl").GetString().Should().Be(avatar.FilePath);

            await using var db = DatabaseFixture.CreateDbContext();
            var stored = await db.Warnings.SingleAsync(w => w.WarningId == warningId);
            stored.EntityId.Should().Be(TestConstants.SecondUserId);
            stored.EntityType.Should().Be(WarningEntityType.Profile);
            stored.EntitySnapshot.Should().Be("[i]О себе[/i] перед выдачей");
            stored.ProfileStatusSnapshot.Should().Be("Статус перед выдачей");
            stored.ProfileAvatarUploadId.Should().Be(uploadId);
        }
        finally
        {
            if (warningId.HasValue)
            {
                await Unseed(warningId.Value);
            }

            await using (var db = DatabaseFixture.CreateDbContext())
            {
                var user = await db.Users.SingleAsync(u => u.UserId == TestConstants.SecondUserId);
                (user.Status, user.Info, user.AvatarUploadId) = (statusBefore, infoBefore, avatarBefore);
                await db.SaveChangesAsync();
            }

            await UnseedUpload(uploadId);
        }
    }
}
