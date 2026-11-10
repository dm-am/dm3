using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Web.API.IntegrationTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Account;

// On a live database: the lookup by address runs past the global filter on removed rows.
public class RemovedAndBannedSignInShould : IntegrationTestBase
{
    public RemovedAndBannedSignInShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private async Task<HttpResponseMessage> SignIn(string email, string password) =>
        await Client.PostAsJsonAsync("/v1/account/login", new { email, password });

    // The owner of a removed account, holding the right password, is told what became of it; that
    // is the one place the site says so (SECURITY.md).
    [Fact]
    public async Task NameARemovedAccountToItsOwnerOnceThePasswordIsProven()
    {
        var (login, email) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, "removed", TestConstants.Password);
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            var user = await db.Users.FirstAsync(u => u.Username == login);
            user.IsRemoved = true;
            await db.SaveChangesAsync();
        }

        var response = await SignIn(email, TestConstants.Password);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        problem["title"]!.GetValue<string>().Should().Be("Аккаунт удален");
    }

    // Without the password the removed account is indistinguishable from an address that was never
    // registered.
    [Fact]
    public async Task HideARemovedAccountBehindTheUnifiedRefusalWithoutThePassword()
    {
        var (login, email) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, "removedwrong", TestConstants.Password);
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            var user = await db.Users.FirstAsync(u => u.Username == login);
            user.IsRemoved = true;
            await db.SaveChangesAsync();
        }

        var response = await SignIn(email, "NotThePassword1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Неверная почта или пароль").And.NotContain("удален");
    }

    // The truth goes to the mailbox; on the screen two guest requests must not combine into
    // "removed".
    [Fact]
    public async Task AnswerRecoveryForARemovedAccountAsForALiveOne()
    {
        var (_, liveEmail) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, "recolive", TestConstants.Password);
        var (goneLogin, goneEmail) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, "recogone", TestConstants.Password);
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            var user = await db.Users.FirstAsync(u => u.Username == goneLogin);
            user.IsRemoved = true;
            await db.SaveChangesAsync();
        }

        var live = await Client.PostAsJsonAsync("/v1/account/recovery", new { email = liveEmail });
        var gone = await Client.PostAsJsonAsync("/v1/account/recovery", new { email = goneEmail });

        gone.StatusCode.Should().Be(live.StatusCode);
        var liveBody = JsonNode.Parse(await live.Content.ReadAsStringAsync())!;
        var goneBody = JsonNode.Parse(await gone.Content.ReadAsStringAsync())!;
        goneBody["status"]!.GetValue<string>().Should().Be(liveBody["status"]!.GetValue<string>());
    }

    // A ban with an end carries that end, so the screen can name the day.
    [Fact]
    public async Task CarryTheEndOfATemporaryBan()
    {
        var (login, email) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, "banned", TestConstants.Password);
        var ends = new DateTimeOffset(2027, 2, 28, 0, 0, 0, TimeSpan.Zero);
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            var user = await db.Users.FirstAsync(u => u.Username == login);
            db.Bans.Add(new Ban
            {
                BanId = Guid.NewGuid(),
                TargetUserId = user.UserId,
                AuthorId = user.UserId,
                StartedUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                EndedUtc = ends,
                Comment = "test",
                AccessRestrictionPolicy = AccessPolicy.FullBan
            });
            await db.SaveChangesAsync();
        }

        var response = await SignIn(email, TestConstants.Password);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        problem["title"]!.GetValue<string>().Should().Be("Аккаунт забанен");
        problem["banExpiresUtc"]!.GetValue<DateTimeOffset>().Should().Be(ends);
    }
}
