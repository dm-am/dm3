using System;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Web.API.IntegrationTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Account;

/// <summary>
/// What the sign-in says about an account that moderation has closed, and when.
/// </summary>
/// <remarks>
/// On a live database, because the unit tests mock the repository and the
/// defect these gates were written for lived in the query: the lookup by
/// address ran under the global filter on removed rows, so a removed account
/// did not exist for the sign-in, the right password earned "wrong email or
/// password", and the removed branch of the service was unreachable. The ban
/// date had a defect of the same family: computed off the guest the refused
/// identity carries, it was always empty.
/// </remarks>
public class RemovedAndBannedSignInShould : IntegrationTestBase
{
    private const string Password = "TestPass123ok";

    public RemovedAndBannedSignInShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private async Task<HttpResponseMessage> SignIn(string email, string password) =>
        await Client.PostAsJsonAsync("/v1/account/login", new { email, password });

    /// <summary>
    /// The owner of a removed account, holding the right password, is told what
    /// became of it; that is the one place the site says so (SECURITY.md).
    /// </summary>
    [Fact]
    public async Task NameARemovedAccountToItsOwnerOnceThePasswordIsProven()
    {
        var (login, email) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, "removed", Password);
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            var user = await db.Users.FirstAsync(u => u.Username == login);
            user.IsRemoved = true;
            await db.SaveChangesAsync();
        }

        var response = await SignIn(email, Password);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        problem["title"]!.GetValue<string>().Should().Be("Аккаунт удален");
    }

    /// <summary>
    /// Without the password the removed account is indistinguishable from an
    /// address that was never registered.
    /// </summary>
    [Fact]
    public async Task HideARemovedAccountBehindTheUnifiedRefusalWithoutThePassword()
    {
        var (login, email) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, "removedwrong", Password);
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

    /// <summary>
    /// Recovery answers a removed account exactly as a live one on the wire.
    /// </summary>
    /// <remarks>
    /// The truth goes to the mailbox. On the screen, "not found" here plus
    /// "taken" on registration equalled "removed" for two guest requests.
    /// </remarks>
    [Fact]
    public async Task AnswerRecoveryForARemovedAccountAsForALiveOne()
    {
        var (liveLogin, liveEmail) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, "recolive", Password);
        var (goneLogin, goneEmail) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, "recogone", Password);
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
        _ = liveLogin;
    }

    /// <summary>
    /// A ban with an end carries that end, so the screen can name the day.
    /// </summary>
    [Fact]
    public async Task CarryTheEndOfATemporaryBan()
    {
        var (login, email) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, "banned", Password);
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

        var response = await SignIn(email, Password);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        problem["title"]!.GetValue<string>().Should().Be("Аккаунт забанен");
        problem["banExpiresUtc"]!.GetValue<DateTimeOffset>().Should().Be(ends);
    }
}
