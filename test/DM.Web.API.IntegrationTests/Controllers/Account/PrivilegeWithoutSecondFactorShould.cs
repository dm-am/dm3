using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Account;

/// <summary>
/// What a senior moderator or an administrator can do without a second factor:
/// everything the rank gives.
/// </summary>
/// <remarks>
/// The factor is optional for every rank. An account that has one switched on
/// is asked for it at sign-in, whatever its rank; an account that has none acts
/// with the rank recorded on it.
///
/// Exercised through a real cookie session rather than the test headers, because
/// the identity under test is the one the sign-in builds, and the test headers
/// build one of their own.
/// </remarks>
public class PrivilegeWithoutSecondFactorShould : IntegrationTestBase
{
    public PrivilegeWithoutSecondFactorShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private const string Password = "TestPass123ok";

    private async Task<(string Login, string Email)> CreatePrivilegedUser(string prefix, UserRole role)
    {
        var (login, email) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, prefix, Password);

        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.Username == login);
        user.Role = role;
        await db.SaveChangesAsync();

        return (login, email);
    }

    /// <summary>
    /// The rank is in force from the first request of a session opened by
    /// password alone.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SeniorModerator)]
    public async Task KeepTheRankWithoutASecondFactor(UserRole role)
    {
        var (_, email) = await CreatePrivilegedUser("privrank", role);
        var session = await UserTestHelper.Login(Client, email, Password);

        var banList = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Get, "/v1/bans", session);
        (await Client.SendAsync(banList)).StatusCode.Should().Be(HttpStatusCode.OK,
            "a second factor is optional for this rank");
    }

    /// <summary>
    /// The viewer and the factor status carry no trace of a withheld rank.
    /// </summary>
    [Fact]
    public async Task SayNothingAboutAWithheldRank()
    {
        var (_, email) = await CreatePrivilegedUser("privflag", UserRole.Admin);
        var session = await UserTestHelper.Login(Client, email, Password);

        var profile = await Client.SendAsync(UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Get, "/v1/users/me/profile", session));
        profile.StatusCode.Should().Be(HttpStatusCode.OK);
        var viewer = JsonNode.Parse(await profile.Content.ReadAsStringAsync())!;
        viewer["privilegeWithheld"].Should().BeNull();
        viewer["role"]!.GetValue<string>().Should().Be("Admin");

        var status = await Client.SendAsync(UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Get, "/v1/account/two-factor", session));
        status.StatusCode.Should().Be(HttpStatusCode.OK);
        var resource = JsonNode.Parse(await status.Content.ReadAsStringAsync())!["resource"]!;
        resource["required"].Should().BeNull();
        resource["privilegeWithheld"].Should().BeNull();
        resource["enabled"]!.GetValue<bool>().Should().BeFalse();
    }

    /// <summary>
    /// An ordinary account short of the rank gets the refusal that names nothing.
    /// </summary>
    [Fact]
    public async Task KeepTheOrdinaryRefusalForAnOrdinaryAccount()
    {
        var (_, email) = await CreatePrivilegedUser("privplain", UserRole.RegularUser);
        var session = await UserTestHelper.Login(Client, email, Password);

        var refused = await Client.SendAsync(
            UserTestHelper.CreateCookieAuthRequest(HttpMethod.Get, "/v1/bans", session));

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Title(refused)).Should().Be(RefusalMessage.AccessDenied);
    }

    private static async Task<string?> Title(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!["title"]?.GetValue<string>();

    /// <summary>
    /// The mailed removal path is open to these ranks like to anyone else.
    /// </summary>
    /// <remarks>
    /// This path is the only way back for a person who has lost both the phone
    /// and the recovery codes.
    /// </remarks>
    [Fact]
    public async Task IssueARemovalLinkForAPrivilegedRank()
    {
        var (login, email) = await CreatePrivilegedUser("privmail", UserRole.Admin);
        var session = await UserTestHelper.Login(Client, email, Password);
        await UserTestHelper.EnableSecondFactor(Client, session, Password);

        var response = await Client.PostAsJsonAsync("/v1/account/two-factor/removal", new { email });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "the answer says nothing about the account it was given");

        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.Username == login);
        (await db.Tokens.CountAsync(t =>
                t.UserId == user.UserId && t.Type == TokenType.TwoFactorRemovalRequest && !t.IsRemoved))
            .Should().Be(1, "the letter is the way back and the rank does not close it");
    }

    /// <summary>
    /// The same request for an ordinary account does issue a link.
    /// </summary>
    /// <remarks>
    /// Beside the test above rather than in place of it: without this one, an
    /// endpoint that issued nothing to anybody would look correct.
    /// </remarks>
    [Fact]
    public async Task IssueARemovalLinkForAnOrdinaryAccount()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var login = $"privord{uid}";
        var email = $"{login}@test.example.com";
        await UserTestHelper.CreateActivatedUser(Client, DatabaseFixture, login, Password, email);
        var session = await UserTestHelper.Login(Client, email, Password);
        await UserTestHelper.EnableSecondFactor(Client, session, Password);

        var response = await Client.PostAsJsonAsync("/v1/account/two-factor/removal", new { email });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.Username == login);
        (await db.Tokens.CountAsync(t =>
                t.UserId == user.UserId && t.Type == TokenType.TwoFactorRemovalRequest && !t.IsRemoved))
            .Should().Be(1);
    }
}
