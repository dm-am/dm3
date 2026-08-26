using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Account;

/// <summary>
/// What a rank that owes a second factor can do before it has one.
/// </summary>
/// <remarks>
/// INV-9: the factor never refuses a login. It withholds the rank, and the only
/// person who can give the rank back is the owner, from an ordinary session, in
/// the time it takes to scan a code - which is why the setup endpoints are not
/// behind the same gate.
///
/// Exercised through a real cookie session rather than the test headers, because
/// the thing under test is the fold that happens while an identity is built, and
/// the test headers build one of their own.
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
    /// AC-19 and AC-20: the rank is withheld, then comes back with the factor
    /// and without a new sign-in.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SeniorModerator)]
    public async Task WithholdTheRankUntilTheFactorIsThere(UserRole role)
    {
        var (login, email) = await CreatePrivilegedUser("privrank", role);
        var session = await UserTestHelper.Login(Client, email, Password);

        // Reads the site as anybody does.
        var ownProfile = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Get, "/v1/users/me/profile", session);
        (await Client.SendAsync(ownProfile)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Anything the rank gives is refused.
        var banList = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Get, "/v1/bans", session);
        (await Client.SendAsync(banList)).StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the account acts with the rights of an ordinary user until it has a factor");

        await UserTestHelper.EnableSecondFactor(Client, session, Password);

        // The same session, the very next request.
        var again = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Get, "/v1/bans", session);
        (await Client.SendAsync(again)).StatusCode.Should().Be(HttpStatusCode.OK,
            "the fold runs where the identity is built, so nothing has to be re-issued");

        login.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// The viewer arrives knowing that the rank is not in force.
    /// </summary>
    /// <remarks>
    /// The interface has to draw the notice and the refusal from the viewer it
    /// already holds. Asking a second endpoint on every page load would be a
    /// request everybody pays for a state two accounts are in, and it would
    /// leave a window in which the moderation tabs are already on screen and the
    /// answer has not come back yet.
    ///
    /// Beside the flag the recorded role travels unchanged (INV-11): the account
    /// stopped being able, not being an administrator.
    /// </remarks>
    [Fact]
    public async Task CarryTheWithheldFlagWithTheViewer()
    {
        var (_, email) = await CreatePrivilegedUser("privflag", UserRole.Admin);
        var session = await UserTestHelper.Login(Client, email, Password);

        var withheld = await ReadOwnProfile(session);
        withheld["privilegeWithheld"]!.GetValue<bool>().Should().BeTrue();
        withheld["role"]!.GetValue<string>().Should().Be("Admin",
            "only the role permissions are compared against is withheld");

        await UserTestHelper.EnableSecondFactor(Client, session, Password);

        var restored = await ReadOwnProfile(session);
        restored["privilegeWithheld"]!.GetValue<bool>().Should().BeFalse(
            "the fold runs on every request, so the notice goes as soon as the factor is on");
    }

    /// <summary>
    /// The sign-in answer carries it too, and it has to.
    /// </summary>
    /// <remarks>
    /// Nothing re-reads the viewer between the sign-in and the first moderation
    /// page: the client adopts the user out of this body and goes on with it
    /// until the next boot.
    /// </remarks>
    [Fact]
    public async Task CarryTheWithheldFlagInTheSignInAnswer()
    {
        var (_, email) = await CreatePrivilegedUser("privlogin", UserRole.Admin);

        var response = await Client.PostAsJsonAsync(
            "/v1/account/login", new { email, password = Password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        body["user"]!["privilegeWithheld"]!.GetValue<bool>().Should().BeTrue();
    }

    /// <summary>
    /// An ordinary account is told it is not withheld, and a stranger is told
    /// nothing at all.
    /// </summary>
    /// <remarks>
    /// The flag is about the reader's own account. Present in an answer about
    /// somebody else it would be a statement about the security of that
    /// account, published to whoever loaded a list of comment authors.
    /// </remarks>
    [Fact]
    public async Task KeepTheWithheldFlagOutOfAnswersAboutOtherPeople()
    {
        var (login, email) = await CreatePrivilegedUser("privquiet", UserRole.Admin);
        var (_, readerEmail) = await CreatePrivilegedUser("privreader", UserRole.RegularUser);
        var readerSession = await UserTestHelper.Login(Client, readerEmail, Password);

        (await ReadOwnProfile(readerSession))["privilegeWithheld"]!.GetValue<bool>()
            .Should().BeFalse("an ordinary rank owes no factor");

        // The withheld administrator, read by somebody else.
        var request = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Get, $"/v1/users/{login}/profile", readerSession);
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var resource = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["resource"]!;
        resource["privilegeWithheld"].Should().BeNull(
            "whether somebody else's rank is withheld is theirs to know");
        email.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// UI-8 and section 4: the refusal a withheld rank gets names its reason.
    /// </summary>
    /// <remarks>
    /// The one refusal by rank in the product that does. "Недостаточно прав" is
    /// not merely unhelpful to an administrator, it is untrue, and the reader it
    /// is untrue to would go looking for a broken site rather than for the
    /// setting that fixes it in a minute.
    /// </remarks>
    [Fact]
    public async Task NameTheReasonWhenTheRankWasWithheld()
    {
        var (_, email) = await CreatePrivilegedUser("privwhy", UserRole.Admin);
        var session = await UserTestHelper.Login(Client, email, Password);

        var refused = await Client.SendAsync(
            UserTestHelper.CreateCookieAuthRequest(HttpMethod.Get, "/v1/bans", session));

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Title(refused)).Should().Be(RefusalMessage.PrivilegeWithheldWithoutTwoFactor);

        await UserTestHelper.EnableSecondFactor(Client, session, Password);
        (await Client.SendAsync(
                UserTestHelper.CreateCookieAuthRequest(HttpMethod.Get, "/v1/bans", session)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// And nobody else gets that sentence.
    /// </summary>
    /// <remarks>
    /// Two ways of being short of a rank that must keep the refusal that names
    /// nothing: an ordinary account, and a withheld senior moderator reaching
    /// for a page that was never his. Told the reason, the second would read it
    /// as a promise that the factor buys the page.
    /// </remarks>
    [Fact]
    public async Task KeepTheOrdinaryRefusalForEverybodyElse()
    {
        var (_, ordinaryEmail) = await CreatePrivilegedUser("privplain", UserRole.RegularUser);
        var ordinary = await UserTestHelper.Login(Client, ordinaryEmail, Password);

        var refusedOrdinary = await Client.SendAsync(
            UserTestHelper.CreateCookieAuthRequest(HttpMethod.Get, "/v1/bans", ordinary));
        refusedOrdinary.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Title(refusedOrdinary)).Should().Be(RefusalMessage.AccessDenied);

        var (senior, seniorEmail) = await CreatePrivilegedUser("privshort", UserRole.SeniorModerator);
        var seniorSession = await UserTestHelper.Login(Client, seniorEmail, Password);

        var refusedSenior = await Client.SendAsync(UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Get, $"/v1/users/{senior}/login-history", seniorSession));
        refusedSenior.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Title(refusedSenior)).Should().Be(RefusalMessage.AccessDenied,
            "the factor would not have opened this page for him either");
    }

    /// <summary>
    /// The own profile, which answers with a bare body rather than an envelope.
    /// </summary>
    private async Task<JsonNode> ReadOwnProfile(string sessionCookie)
    {
        var request = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Get, "/v1/users/me/profile", sessionCookie);
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    }

    private static async Task<string?> Title(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!["title"]?.GetValue<string>();

    /// <summary>
    /// AC-21: the role a reader sees is the one recorded on the account.
    /// </summary>
    /// <remarks>
    /// A moderator displayed - and journalled - as an ordinary user is a record
    /// nothing can be reconstructed from afterwards. Only the role that
    /// permissions are compared against is withheld.
    /// </remarks>
    [Fact]
    public async Task ShowTheRecordedRoleWhileTheRankIsWithheld()
    {
        var (login, email) = await CreatePrivilegedUser("privshow", UserRole.Admin);
        var session = await UserTestHelper.Login(Client, email, Password);

        var request = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Get, $"/v1/users/{login}", session);
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Admin");
    }

    /// <summary>
    /// INV-9: the way out is never closed - the setup path stays open to the
    /// account whose rank is withheld.
    /// </summary>
    [Fact]
    public async Task LeaveTheSetupPathOpenToAWithheldAccount()
    {
        var (_, email) = await CreatePrivilegedUser("privsetup", UserRole.Admin);
        var session = await UserTestHelper.Login(Client, email, Password);

        var status = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Get, "/v1/account/two-factor", session);
        var response = await Client.SendAsync(status);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var resource = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["resource"]!;
        resource["required"]!.GetValue<bool>().Should().BeTrue();
        resource["privilegeWithheld"]!.GetValue<bool>().Should().BeTrue();
        resource["enabled"]!.GetValue<bool>().Should().BeFalse();
    }

    /// <summary>
    /// INV-12 and AC-22: the mailed removal path is closed for these ranks.
    /// </summary>
    /// <remarks>
    /// The answer is the same for every address - which accounts have a factor is
    /// not something an anonymous endpoint may disclose - so what is asserted is
    /// that nothing was issued.
    /// </remarks>
    [Fact]
    public async Task IssueNoRemovalLinkForARankThatOwesAFactor()
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
            .Should().Be(0, "their factor is taken off by the second administrator and by nothing else");
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
