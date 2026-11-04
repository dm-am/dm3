using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Tokens;
using DM.Infrastructure.Persistence.Entities.Account;
using TokenEntity = DM.Infrastructure.Persistence.Entities.Account.Token;
using SecurityAuditEntryEntity = DM.Infrastructure.Persistence.Entities.Account.SecurityAuditEntry;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using DM.Web.API.Shared.Http;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Account;

/// <summary>
/// The second factor from the outside: switched on, passed, replayed, recovered.
/// </summary>
/// <remarks>Over the real database: a step claim and a code spend are conditional updates.</remarks>
public class TwoFactorShould : IntegrationTestBase
{
    public TwoFactorShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private sealed record Account(string Login, string Email, string SessionCookie);

    private async Task<Account> CreateUser(string prefix)
    {
        var (login, email) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, prefix, TestConstants.Password);
        var cookie = await UserTestHelper.Login(Client, email, TestConstants.Password);
        return new Account(login, email, cookie);
    }

    /// <summary>The step a moment falls in, and the code for that step.</summary>
    private static (long Step, string Code) CodeFor(string base32Secret, DateTime moment)
    {
        var totp = new Totp(Base32Encoding.ToBytes(base32Secret));
        return (new DateTimeOffset(moment, TimeSpan.Zero).ToUnixTimeSeconds() / 30,
            totp.ComputeTotp(moment));
    }

    /// <summary>
    /// A code for the step after the current one.
    /// </summary>
    /// <remarks>One step ahead is inside the window and never the step a previous call claimed.</remarks>
    private static (long Step, string Code) NextCode(string base32Secret) =>
        CodeFor(base32Secret, DateTime.UtcNow.AddSeconds(30));

    private static HttpRequestMessage WithCookies(
        HttpMethod method, string url, params (string Name, string Value)[] cookies)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Cookie",
            string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}")));
        return request;
    }

    /// <summary>Switches the factor on: the Base32 secret and the recovery codes the confirmation issues.</summary>
    private Task<(string Secret, IReadOnlyList<string> Codes)> EnableFactor(Account account) =>
        UserTestHelper.EnableSecondFactor(Client, account.SessionCookie, TestConstants.Password);

    /// <summary>The first step of a login, for an account that owes a factor.</summary>
    private async Task<string> BeginLogin(Account account)
    {
        var response = await Client.PostAsJsonAsync(
            "/v1/account/login", new { email = account.Email, password = TestConstants.Password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        body["twoFactorRequired"]!.GetValue<bool>().Should().BeTrue();
        body["user"]?.GetValue<object?>().Should().BeNull("the login is not finished");

        // No session exists between the two factors.
        UserTestHelper.Cookie(response, "dm_session").Should().BeNull();

        var challenge = UserTestHelper.Cookie(response, "dm_2fa");
        challenge.Should().NotBeNullOrEmpty();
        return challenge!;
    }

    private Task<HttpResponseMessage> FinishLogin(string challenge, string code)
    {
        var request = WithCookies(
            HttpMethod.Post, "/v1/account/login/two-factor", ("dm_2fa", challenge));
        request.Content = JsonContent.Create(new { code });
        return Client.SendAsync(request);
    }

    /// <summary>The answer carries the secret and the URI, once.</summary>
    [Fact]
    public async Task HandOverASecretAndAUriAndLeaveTheLoginSingleFactor()
    {
        var account = await CreateUser("tfsetup");

        var setup = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Post, "/v1/account/two-factor/setup", account.SessionCookie);
        setup.Content = JsonContent.Create(new { password = TestConstants.Password });
        var response = await Client.SendAsync(setup);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var resource = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["resource"]!;
        resource["secret"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        resource["otpAuthUri"]!.GetValue<string>().Should().StartWith("otpauth://totp/");

        // The factor is off until it is confirmed, so the login is unchanged.
        var login = await Client.PostAsJsonAsync(
            "/v1/account/login", new { email = account.Email, password = TestConstants.Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        UserTestHelper.Cookie(login, "dm_session").Should().NotBeNullOrEmpty();
    }

    /// <summary>The current password is not optional.</summary>
    [Fact]
    public async Task RefuseASetupWithoutTheCurrentPassword()
    {
        var account = await CreateUser("tfnopass");

        var setup = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Post, "/v1/account/two-factor/setup", account.SessionCookie);
        setup.Content = JsonContent.Create(new { password = "WrongPassword123" });

        (await Client.SendAsync(setup)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// What the table holds is an envelope, and the secret is inside it.
    /// </summary>
    [Fact]
    public async Task StoreTheSecretOnlyInsideTheEnvelope()
    {
        var account = await CreateUser("tfstore");
        var (secret, _) = await EnableFactor(account);

        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.Username == account.Login);
        var stored = await db.Set<UserTwoFactor>().FirstAsync(f => f.UserId == user.UserId);

        stored.Secret.Should().NotContain(secret,
            "the column carries the AEAD envelope, and a reader of the dump without the " +
            "application key gets nothing out of it");
        stored.ConfirmedUtc.Should().NotBeNull();
    }

    /// <summary>Confirming ends every other session.</summary>
    [Fact]
    public async Task EndEveryOtherSessionOnConfirmation()
    {
        var account = await CreateUser("tfothers");
        var other = await UserTestHelper.Login(Client, account.Email, TestConstants.Password);

        await EnableFactor(account);

        var check = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Get, "/v1/users/me/profile", other);
        (await Client.SendAsync(check)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A good code finishes the login and mints the session.</summary>
    [Fact]
    public async Task FinishTheLoginWithACodeFromTheDevice()
    {
        var account = await CreateUser("tflogin");
        var (secret, _) = await EnableFactor(account);

        var challenge = await BeginLogin(account);
        var (step, code) = NextCode(secret);
        var response = await FinishLogin(challenge, code);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        UserTestHelper.Cookie(response, "dm_session").Should().NotBeNullOrEmpty();

        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.Username == account.Login);
        var stored = await db.Set<UserTwoFactor>().FirstAsync(f => f.UserId == user.UserId);
        stored.LastAcceptedStep.Should().Be(step,
            "the accepted step is what the replay guard remembers");
    }

    /// <summary>The same code is not accepted twice.</summary>
    [Fact]
    public async Task RefuseACodeWhoseStepWasAlreadyAccepted()
    {
        var account = await CreateUser("tfreplay");
        var (secret, _) = await EnableFactor(account);

        var firstChallenge = await BeginLogin(account);
        var (_, code) = NextCode(secret);
        (await FinishLogin(firstChallenge, code)).StatusCode.Should().Be(HttpStatusCode.OK);

        // A code lifted off a phishing proxy or read over a shoulder is
        // arithmetically valid for up to ninety seconds. The step it belongs to
        // is what stops it, across sessions and across challenges.
        var secondChallenge = await BeginLogin(account);
        var replay = await FinishLogin(secondChallenge, code);

        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        UserTestHelper.Cookie(replay, "dm_session").Should().BeNull();
    }

    /// <summary>A recovery code opens the account exactly once.</summary>
    [Fact]
    public async Task SpendARecoveryCodeExactlyOnce()
    {
        var account = await CreateUser("tfrecov");
        var (_, codes) = await EnableFactor(account);
        codes.Should().HaveCount(10);

        var challenge = await BeginLogin(account);
        var first = await FinishLogin(challenge, codes[0]);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        UserTestHelper.Cookie(first, "dm_session").Should().NotBeNullOrEmpty();

        var secondChallenge = await BeginLogin(account);
        var second = await FinishLogin(secondChallenge, codes[0]);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.Username == account.Login);
        (await db.Set<UserTwoFactorRecoveryCode>()
                .CountAsync(c => c.UserId == user.UserId && c.UsedUtc == null))
            .Should().Be(9, "a spent code is stamped rather than deleted");
    }

    /// <summary>
    /// The table holds the hash of a code and never the code.
    /// </summary>
    [Fact]
    public async Task StoreOnlyTheHashOfARecoveryCode()
    {
        var account = await CreateUser("tfrhash");
        var (_, codes) = await EnableFactor(account);

        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.Username == account.Login);
        var rows = await db.Set<UserTwoFactorRecoveryCode>()
            .Where(c => c.UserId == user.UserId)
            .ToListAsync();

        rows.Should().OnlyContain(row => row.CodeHash.Length == 32);
        var stored = string.Join(",", rows.Select(row => Convert.ToBase64String(row.CodeHash)));
        stored.Should().NotContainAny(codes.ToArray());
    }

    /// <summary>A reissue retires the previous set whole.</summary>
    [Fact]
    public async Task InvalidateEveryCodeOfThePreviousSetOnReissue()
    {
        var account = await CreateUser("tfreissue");
        var (_, old) = await EnableFactor(account);

        var reissue = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Post, "/v1/account/two-factor/recovery-codes", account.SessionCookie);
        reissue.Content = JsonContent.Create(new { password = TestConstants.Password, code = old[9] });
        var reissued = await Client.SendAsync(reissue);
        reissued.StatusCode.Should().Be(HttpStatusCode.OK);

        var fresh = JsonNode.Parse(await reissued.Content.ReadAsStringAsync())!
            ["resource"]!["codes"]!.AsArray().Select(node => node!.GetValue<string>()).ToList();
        fresh.Should().NotIntersectWith(old);

        // A code crossed off on paper must not still open the account.
        var challenge = await BeginLogin(account);
        (await FinishLogin(challenge, old[0])).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var second = await BeginLogin(account);
        (await FinishLogin(second, fresh[0])).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The challenge is destroyed once the attempts run out.</summary>
    [Fact]
    public async Task DestroyTheChallengeWhenTheAttemptsRunOut()
    {
        var account = await CreateUser("tflimit");
        var (secret, _) = await EnableFactor(account);

        var challenge = await BeginLogin(account);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await FinishLogin(challenge, "000000")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // Past the limit even the right code is refused: the challenge is gone,
        // and carrying on costs the password again.
        var (_, code) = NextCode(secret);
        (await FinishLogin(challenge, code)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.Username == account.Login);
        (await db.Set<TwoFactorChallenge>().CountAsync(c => c.UserId == user.UserId))
            .Should().Be(0);
    }

    /// <summary>
    /// Five ways of failing, one answer.
    /// </summary>
    /// <remarks>Compared with the correlation token taken out: it differs in every response by design.</remarks>
    [Fact]
    public async Task AnswerEveryFailureOfTheSecondFactorTheSameWay()
    {
        var account = await CreateUser("tfsame");
        var (_, codes) = await EnableFactor(account);

        var wrongCode = await FinishLogin(await BeginLogin(account), "000000");

        var unknownChallenge = await FinishLogin(
            await BeginLogin(account), "000000");

        var spent = await BeginLogin(account);
        (await FinishLogin(spent, codes[0])).StatusCode.Should().Be(HttpStatusCode.OK);
        var spentCode = await FinishLogin(await BeginLogin(account), codes[0]);

        var noCookie = new HttpRequestMessage(HttpMethod.Post, "/v1/account/login/two-factor")
        {
            Content = JsonContent.Create(new { code = "000000" })
        };
        var missing = await Client.SendAsync(noCookie);

        var bodies = new List<string>();
        foreach (var response in new[] { wrongCode, unknownChallenge, spentCode, missing })
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            bodies.Add(WithoutCorrelation(await response.Content.ReadAsStringAsync()));
        }

        bodies.Distinct(StringComparer.Ordinal).Should().ContainSingle(
            "the number of tries left, and which wall the caller is standing at, are " +
            "exactly what a guess must not learn");
    }

    private static string WithoutCorrelation(string body)
    {
        var document = JsonNode.Parse(body)!.AsObject();
        document.Remove("traceId");
        return document.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// The mailed password reset hands out no session and lifts no factor.
    /// </summary>
    [Fact]
    public async Task LeaveTheFactorStandingAfterAPasswordResetFromTheMailbox()
    {
        var account = await CreateUser("tfreset");
        await EnableFactor(account);

        await using (var db = DatabaseFixture.CreateDbContext())
        {
            var user = await db.Users.FirstAsync(u => u.Username == account.Login);
            var secret = Guid.NewGuid();
            db.Tokens.Add(new TokenEntity
            {
                TokenId = Guid.NewGuid(),
                UserId = user.UserId,
                Type = DM.Domain.Core.Enums.TokenType.PasswordChange,
                CreatedUtc = DateTimeOffset.UtcNow,
                SecretHash = ConfirmationSecret.Hash(secret)
            });
            await db.SaveChangesAsync();

            var reset = new HttpRequestMessage(HttpMethod.Post, "/v1/account/password-reset")
            {
                Content = JsonContent.Create(new { newPassword = "AnotherPass456ok" })
            };
            reset.Headers.Add(TokenHeaders.Account, secret.ToString());
            var response = await Client.SendAsync(reset);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            UserTestHelper.Cookie(response, "dm_session").Should().BeNull("a reset does not sign anybody in");
        }

        // And the next login still owes the factor.
        var login = await Client.PostAsJsonAsync(
            "/v1/account/login", new { email = account.Email, password = "AnotherPass456ok" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonNode.Parse(await login.Content.ReadAsStringAsync())!
            ["twoFactorRequired"]!.GetValue<bool>().Should().BeTrue();
        UserTestHelper.Cookie(login, "dm_session").Should().BeNull();
    }

    /// <summary>A password change kills unfinished logins.</summary>
    [Fact]
    public async Task DiscardUnfinishedLoginsWhenThePasswordChanges()
    {
        var account = await CreateUser("tfpwd");
        var (secret, _) = await EnableFactor(account);

        var challenge = await BeginLogin(account);

        var change = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Post, "/v1/account/password", account.SessionCookie);
        change.Content = JsonContent.Create(
            new { oldPassword = TestConstants.Password, newPassword = "AnotherPass456ok" });
        (await Client.SendAsync(change)).StatusCode.Should().Be(HttpStatusCode.OK);

        var (_, code) = NextCode(secret);
        (await FinishLogin(challenge, code)).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "a change of password is a statement that the account may be compromised, and " +
            "a login begun with the old one must not survive it");
    }

    /// <summary>
    /// Nothing that can pass the factor reaches the security journal.
    /// </summary>
    [Fact]
    public async Task WriteNeitherSecretNorRecoveryCodeIntoTheJournal()
    {
        var account = await CreateUser("tfjournal");
        var (secret, codes) = await EnableFactor(account);

        var challenge = await BeginLogin(account);
        (await FinishLogin(challenge, codes[0])).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.Username == account.Login);
        var journal = await db.Set<SecurityAuditEntryEntity>()
            .Where(e => e.UserId == user.UserId)
            .ToListAsync();

        journal.Should().NotBeEmpty("switching the factor on is an event the owner reads");
        var written = string.Join("\n", journal.Select(e => $"{e.Details}|{e.IpAddress}|{e.UserAgent}"));
        written.Should().NotContain(secret);
        written.Should().NotContainAny(codes.ToArray());
        journal.Select(e => e.EventType).Should().Contain(SecurityEventType.TwoFactorEnabled);
        journal.Select(e => e.EventType).Should().Contain(SecurityEventType.TwoFactorRecoveryCodeUsed);
    }

    /// <summary>
    /// The entry about a spent recovery code says how many are left.
    /// </summary>
    /// <remarks>The count is taken after the row is stamped, an order only the real database shows.</remarks>
    [Fact]
    public async Task SayHowManyRecoveryCodesAreLeftInTheJournal()
    {
        var account = await CreateUser("tfleft");
        var (_, codes) = await EnableFactor(account);

        var challenge = await BeginLogin(account);
        (await FinishLogin(challenge, codes[0])).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.Username == account.Login);
        var spent = await db.Set<SecurityAuditEntryEntity>()
            .Where(e => e.UserId == user.UserId &&
                        e.EventType == SecurityEventType.TwoFactorRecoveryCodeUsed)
            .ToListAsync();

        spent.Should().ContainSingle().Which.Details.Should().Be("Осталось резервных кодов: 9");
    }

    /// <summary>
    /// Switching the factor off costs the password and a second factor.
    /// </summary>
    [Fact]
    public async Task SwitchTheFactorOffOnlyForBothHalves()
    {
        var account = await CreateUser("tfoff");
        var (_, codes) = await EnableFactor(account);

        var withoutFactor = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Post, "/v1/account/two-factor/disable", account.SessionCookie);
        withoutFactor.Content = JsonContent.Create(new { password = TestConstants.Password, code = "000000" });
        (await Client.SendAsync(withoutFactor)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var withoutPassword = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Post, "/v1/account/two-factor/disable", account.SessionCookie);
        withoutPassword.Content = JsonContent.Create(new { password = "WrongPassword123", code = codes[0] });
        (await Client.SendAsync(withoutPassword)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var both = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Post, "/v1/account/two-factor/disable", account.SessionCookie);
        both.Content = JsonContent.Create(new { password = TestConstants.Password, code = codes[0] });
        (await Client.SendAsync(both)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // And the login is single-factor again.
        var login = await Client.PostAsJsonAsync(
            "/v1/account/login", new { email = account.Email, password = TestConstants.Password });
        UserTestHelper.Cookie(login, "dm_session").Should().NotBeNullOrEmpty();

        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.Username == account.Login);
        (await db.Set<UserTwoFactorRecoveryCode>().CountAsync(c => c.UserId == user.UserId))
            .Should().Be(0, "the codes go with the factor they belonged to");
    }

    /// <summary>
    /// The status of the caller's own factor, and nothing that could pass it.
    /// </summary>
    [Fact]
    public async Task DescribeTheFactorWithoutDisclosingAnythingThatPassesIt()
    {
        var account = await CreateUser("tfstatus");
        var (secret, _) = await EnableFactor(account);

        var request = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Get, "/v1/account/two-factor", account.SessionCookie);
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(secret);

        var resource = JsonNode.Parse(body)!["resource"]!;
        resource["enabled"]!.GetValue<bool>().Should().BeTrue();
        resource["recoveryCodesLeft"]!.GetValue<int>().Should().Be(10);
    }
}
