using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using DM.Domain.Core.Dto;
using DM.Web.API.IntegrationTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

/// <remarks>
/// On a live database: both endpoints read past the global filter. The list runs over accounts this test removed,
/// found by a term nobody else uses.
/// </remarks>
public class RemovedAccountsShould : IntegrationTestBase
{
    public RemovedAccountsShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static HttpRequestMessage SeniorModerator(HttpMethod method, string url) =>
        CreateAuthenticatedRequest(method, url, CustomWebApplicationFactory.CreateSeniorModeratorUser());

    // Written to the row: a third endpoint's rules must not decide these tests.
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
            Client, DatabaseFixture, "back", TestConstants.Password);
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

    // Read without lifting the filter, a restoration would report success over the live row.
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
        var (gone, _) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, marker, TestConstants.Password);
        var (live, _) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, marker, TestConstants.Password);
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
    [Fact]
    public async Task ShowTheAddressesToAnAdministratorAndToNobodyBelowOne()
    {
        var marker = "ad" + Guid.NewGuid().ToString("N")[..6];
        var (gone, email) = await UserTestHelper.CreateUniqueUser(
            Client, DatabaseFixture, marker, TestConstants.Password);
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

    public static TheoryData<string, string> Surfaces => new()
    {
        { "POST", $"/v1/moderation/users/{TestConstants.TestUserLogin}/account" },
        { "GET", "/v1/moderation/users/removed" },
    };

    // A moderator refused distinguishes the rank of these from the rank of the screen.
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
    /// <remarks>The domain's rank check reads a guest as a rank below the bar, so the role attribute answers 401.</remarks>
    [Theory]
    [MemberData(nameof(Surfaces))]
    public async Task AnswerAStrangerWithAnUnauthorized(string method, string url)
    {
        var response = await Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
