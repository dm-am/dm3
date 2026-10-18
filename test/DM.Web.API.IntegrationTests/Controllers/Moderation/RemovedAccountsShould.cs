using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Domain.Core.Dto;
using DM.Web.API.IntegrationTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

/// <summary>
/// The two surfaces a removed account is reachable through, and nothing else is.
/// </summary>
/// <remarks>
/// On a live database, because what is being asserted is the half a unit test
/// cannot see. A removed row is hidden by a global query filter, so both of these
/// endpoints read past it, and a query that forgets to lift the filter answers an
/// empty list and a 404 while every mock in the domain suite goes on returning the
/// account it was told to return.
///
/// The list is exercised against accounts this test made and removed itself rather
/// than against the seeded ones: the suite shares one database, so "the removed
/// accounts" is not a fixed set, and only a term nobody else can have used says
/// anything about who is on the page.
/// </remarks>
public class RemovedAccountsShould : IntegrationTestBase
{
    private const string Password = "TestPass123ok";

    public RemovedAccountsShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>The rank both surfaces are behind.</summary>
    private static HttpRequestMessage SeniorModerator(HttpMethod method, string url) =>
        CreateAuthenticatedRequest(method, url, CustomWebApplicationFactory.CreateSeniorModeratorUser());

    /// <summary>
    /// Takes an account out of the product the way a removal does.
    /// </summary>
    /// <remarks>
    /// Written to the row rather than driven through DELETE .../account on
    /// purpose: what is under test here is what the two new surfaces do with an
    /// account that is already gone, and going through a third endpoint would let
    /// its own rank rules and its own letter decide whether these pass.
    /// </remarks>
    private async Task Remove(string login)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.Username == login);
        user.IsRemoved = true;
        await db.SaveChangesAsync();
    }

    /// <summary>One page of the removed accounts, as the given caller sees it.</summary>
    private async Task<string> RemovedPage(string search, GeneralUser caller)
    {
        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/moderation/users/removed?search={search}", caller));

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);
        return body;
    }

    [Fact]
    public async Task BringARemovedAccountBackAndTellItsOwner()
    {
        var (login, email) = await UserTestHelper.CreateUniqueUser(
            Client, DatabaseFixture, "back", Password);
        await Remove(login);

        var response = await Client.SendAsync(SeniorModerator(
            HttpMethod.Post, $"/v1/moderation/users/{login}/account"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "body was: {0}", await response.Content.ReadAsStringAsync());

        await using var db = DatabaseFixture.CreateDbContext();
        var account = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.Username == login);
        account.IsRemoved.Should().BeFalse(
            "the account answering again is the whole of what a restoration is");

        RecordingMailSender.To(email).Select(letter => letter.Subject)
            .Should().Contain(subject => subject.Contains("восстановлен", StringComparison.Ordinal),
                "the owner asked support and watched nothing happen: the letter is the only " +
                "thing that tells him his account is back");
    }

    /// <summary>
    /// A name nobody removed is a 404, and a live account is such a name.
    /// </summary>
    /// <remarks>
    /// The interesting half is the live account. Read without lifting the global
    /// filter, a restoration would find the live row under that name and report
    /// success over an account it did nothing to.
    /// </remarks>
    [Fact]
    public async Task RefuseToRestoreAnAccountNobodyRemoved()
    {
        var response = await Client.SendAsync(SeniorModerator(
            HttpMethod.Post, $"/v1/moderation/users/{TestConstants.TestUserLogin}/account"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListTheRemovedAccountsAndNobodyElse()
    {
        // Shared by both logins and by both addresses, and used by nothing else in
        // the run, so the page it asks for is exactly these two candidates.
        var marker = "rm" + Guid.NewGuid().ToString("N")[..6];
        var (gone, _) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, marker, Password);
        var (live, _) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, marker, Password);
        await Remove(gone);

        using var document = JsonDocument.Parse(
            await RemovedPage(marker, CustomWebApplicationFactory.CreateSeniorModeratorUser()));

        var names = document.RootElement.GetProperty("resources").EnumerateArray()
            .Select(account => account.GetProperty("username").GetString())
            .ToArray();

        names.Should().ContainSingle(
                "the list exists to show the accounts no other list can, and {0} was never removed",
                live)
            .Which.Should().Be(gone);
        document.RootElement.GetProperty("paging").GetProperty("total").GetInt32().Should().Be(1,
            "a caller cannot tell a full page from a truncated one without the total");
    }

    /// <summary>
    /// The address is an administrator's to read, here as on the moderated profile.
    /// </summary>
    /// <remarks>
    /// A list is where this costs the most: the profile hands over one address at a
    /// time, and a page hands over twenty.
    /// </remarks>
    [Fact]
    public async Task ShowTheAddressesToAnAdministratorAndToNobodyBelowOne()
    {
        var marker = "ad" + Guid.NewGuid().ToString("N")[..6];
        var (gone, email) = await UserTestHelper.CreateUniqueUser(
            Client, DatabaseFixture, marker, Password);
        await Remove(gone);

        using var withheld = JsonDocument.Parse(
            await RemovedPage(marker, CustomWebApplicationFactory.CreateSeniorModeratorUser()));
        withheld.RootElement.GetProperty("resources").EnumerateArray().Single()
            .TryGetProperty("email", out _).Should().BeFalse(
                "a senior moderator reads a removed profile without the address, and a page " +
                "of them must not be the way around that");

        using var shown = JsonDocument.Parse(
            await RemovedPage(marker, CustomWebApplicationFactory.CreateAdminUser()));
        shown.RootElement.GetProperty("resources").EnumerateArray().Single()
            .GetProperty("email").GetString().Should().Be(email,
                "the address is what a support letter is answered against, and an " +
                "administrator is who answers it");
    }

    /// <summary>Both surfaces, as a caller below the rank reaches them.</summary>
    public static TheoryData<string, string> Surfaces => new()
    {
        { "POST", $"/v1/moderation/users/{TestConstants.TestUserLogin}/account" },
        { "GET", "/v1/moderation/users/removed" },
    };

    /// <summary>
    /// A moderator is refused as flatly as a reader is.
    /// </summary>
    /// <remarks>
    /// The moderator is the case worth spending a request on: a reader being
    /// refused proves only that something guards the endpoint, while a moderator
    /// being refused is what distinguishes the rank these were given from the rank
    /// of the screen they will sit on.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Surfaces))]
    public async Task RefuseBothSurfacesBelowASeniorModerator(string method, string url)
    {
        foreach (var caller in new[]
                 {
                     CustomWebApplicationFactory.CreateTestUser(),
                     CustomWebApplicationFactory.CreateModeratorUser()
                 })
        {
            var response = await Client.SendAsync(
                CreateAuthenticatedRequest(new HttpMethod(method), url, caller));

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
                "{0} may neither remove an account nor bring one back", caller.Role);
        }
    }

    /// <summary>
    /// A stranger is told he is not signed in, not that he is not allowed.
    /// </summary>
    /// <remarks>
    /// The one refusal the domain cannot give. Its own rank check reads the
    /// identity it is handed, and a guest is simply a rank below the bar, so
    /// without the role attribute on the action an anonymous caller is answered
    /// 403 - told the page is not for him rather than that he never signed in.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Surfaces))]
    public async Task AnswerAStrangerWithAnUnauthorized(string method, string url)
    {
        var response = await Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
