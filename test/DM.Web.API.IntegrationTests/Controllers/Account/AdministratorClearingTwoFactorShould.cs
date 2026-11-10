using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Account;

// The administrator gets no session or right of the account; its sessions end and both journals say
// so.
public class AdministratorClearingTwoFactorShould : IntegrationTestBase
{
    public AdministratorClearingTwoFactorShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private async Task<(string Login, string Email, string Session)> CreateUser(
        string prefix, UserRole role)
    {
        var (login, email) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, prefix, TestConstants.Password);

        if (role != UserRole.RegularUser)
        {
            await using var db = DatabaseFixture.CreateDbContext();
            var user = await db.Users.FirstAsync(u => u.Username == login);
            user.Role = role;
            await db.SaveChangesAsync();
        }

        return (login, email, await UserTestHelper.Login(Client, email, TestConstants.Password));
    }

    [Fact]
    public async Task TakeAColleaguesFactorOffAndEndTheirSessions()
    {
        var target = await CreateUser("tfvictim", UserRole.SeniorModerator);
        await UserTestHelper.EnableSecondFactor(Client, target.Session, TestConstants.Password);

        var admin = await CreateUser("tfadmin", UserRole.Admin);
        await UserTestHelper.EnableSecondFactor(Client, admin.Session, TestConstants.Password);

        var request = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Delete, $"/v1/account/two-factor/users/{target.Login}", admin.Session);
        (await Client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var db = DatabaseFixture.CreateDbContext();
        var victim = await db.Users.FirstAsync(u => u.Username == target.Login);
        var actor = await db.Users.FirstAsync(u => u.Username == admin.Login);

        (await db.Set<UserTwoFactor>().CountAsync(f => f.UserId == victim.UserId)).Should().Be(0);
        (await db.Set<UserSession>().CountAsync(s => s.UserId == victim.UserId)).Should().Be(0,
            "the case this exists for is a stolen account, and ending its sessions is the point");

        // Both journals: the owner has to see what was done to his account, and
        // the administrator's own trail has to carry what he did.
        var written = await db.Set<SecurityAuditEntry>()
            .Where(e => e.UserId == victim.UserId || e.UserId == actor.UserId)
            .Where(e => e.EventType == DM.Domain.Account.Features.Security.SecurityEventType
                .TwoFactorRemovedByAdmin)
            .Select(e => e.UserId)
            .ToListAsync();
        written.Should().Contain([victim.UserId, actor.UserId]);

        // And the administrator is still himself: the action hands out no session.
        var whoAmI = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Get, "/v1/users/me/profile", admin.Session);
        var profile = await Client.SendAsync(whoAmI);
        profile.StatusCode.Should().Be(HttpStatusCode.OK);
        (await profile.Content.ReadAsStringAsync()).Should().Contain(admin.Login);

        // The owner gets back in by password, and owes no factor any more.
        var login = await Client.PostAsJsonAsync(
            "/v1/account/login", new { email = target.Email, password = TestConstants.Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonNode.Parse(await login.Content.ReadAsStringAsync())!["twoFactorRequired"]!
            .GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task RefuseAnybodyBelowAdministrator()
    {
        var target = await CreateUser("tfnovictim", UserRole.SeniorModerator);
        await UserTestHelper.EnableSecondFactor(Client, target.Session, TestConstants.Password);

        var moderator = await CreateUser("tfmod", UserRole.Moderator);

        var request = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Delete, $"/v1/account/two-factor/users/{target.Login}", moderator.Session);
        (await Client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await using var db = DatabaseFixture.CreateDbContext();
        var victim = await db.Users.FirstAsync(u => u.Username == target.Login);
        (await db.Set<UserTwoFactor>().CountAsync(f => f.UserId == victim.UserId)).Should().Be(1);
    }
}
