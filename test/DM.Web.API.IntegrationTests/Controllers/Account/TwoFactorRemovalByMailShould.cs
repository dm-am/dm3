using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Tokens;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Account;

/// <summary>
/// The mailed way back in, for somebody who has lost the device and the codes.
/// </summary>
/// <remarks>
/// INV-13. The letter does not take the factor off. Following the link schedules
/// the removal a week out, ends every session and hands back a way to call it
/// off; the other way to call it off is simply to sign in with the factor. So
/// whoever holds both the mailbox and the password still gets the account - after
/// a week of visible, cancellable noise rather than instantly and silently.
/// </remarks>
public class TwoFactorRemovalByMailShould : IntegrationTestBase
{
    public TwoFactorRemovalByMailShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private const string Password = "TestPass123ok";

    private sealed record Account(string Login, string Email, string Session, string Secret);

    private async Task<Account> CreateUserWithFactor(string prefix)
    {
        var (login, email) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, prefix, Password);
        var session = await UserTestHelper.Login(Client, email, Password);

        var setup = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Post, "/v1/account/two-factor/setup", session);
        setup.Content = JsonContent.Create(new { password = Password });
        var secret = JsonNode.Parse(await (await Client.SendAsync(setup)).Content.ReadAsStringAsync())!
            ["resource"]!["secret"]!.GetValue<string>();

        var confirm = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Post, "/v1/account/two-factor/confirm", session);
        confirm.Content = JsonContent.Create(
            new { code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(DateTime.UtcNow) });
        (await Client.SendAsync(confirm)).StatusCode.Should().Be(HttpStatusCode.OK);

        return new Account(login, email, session, secret);
    }

    /// <summary>
    /// Stamps a known secret onto the live token of a type and returns it.
    /// </summary>
    /// <remarks>
    /// The row keeps only a hash of the value the letter carried, which is the
    /// property asserted elsewhere - so the test issues a value of its own and
    /// stamps its hash, the way the activation helper does.
    /// </remarks>
    private async Task<Guid> TakeOverToken(string login, TokenType type)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.Username == login);
        var token = await db.Tokens
            .Where(t => t.UserId == user.UserId && t.Type == type && !t.IsRemoved)
            .OrderByDescending(t => t.CreatedUtc)
            .FirstAsync();

        var secret = Guid.NewGuid();
        token.SecretHash = ConfirmationSecret.Hash(secret);
        await db.SaveChangesAsync();
        return secret;
    }

    private Task<HttpResponseMessage> Follow(string url, Guid secret)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("X-Dm-Account-Token", secret.ToString());
        return Client.SendAsync(request);
    }

    /// <summary>
    /// INV-13: the link schedules, it does not perform, and it ends the sessions.
    /// </summary>
    [Fact]
    public async Task ScheduleTheRemovalAndEndEverySession()
    {
        var account = await CreateUserWithFactor("tfmailsch");

        (await Client.PostAsJsonAsync("/v1/account/two-factor/removal", new { account.Email }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secret = await TakeOverToken(account.Login, TokenType.TwoFactorRemovalRequest);
        (await Follow("/v1/account/two-factor/removal/confirm", secret))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.Username == account.Login);
        var state = await db.Set<UserTwoFactor>().FirstAsync(f => f.UserId == user.UserId);

        state.ConfirmedUtc.Should().NotBeNull("the factor is still on: the link only scheduled");
        state.RemovalDueUtc.Should().NotBeNull();
        state.RemovalDueUtc!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(7), TimeSpan.FromMinutes(5));
        (await db.Set<UserSession>().CountAsync(s => s.UserId == user.UserId)).Should().Be(0);

        // The old session is dead, and the login still owes the factor.
        var check = UserTestHelper.CreateCookieAuthRequest(
            HttpMethod.Get, "/v1/users/me/profile", account.Session);
        (await Client.SendAsync(check)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A second link comes with the letter, and it calls the removal off.</summary>
    [Fact]
    public async Task CallTheRemovalOffFromTheSecondLink()
    {
        var account = await CreateUserWithFactor("tfmailcancel");

        await Client.PostAsJsonAsync("/v1/account/two-factor/removal", new { account.Email });
        var request = await TakeOverToken(account.Login, TokenType.TwoFactorRemovalRequest);
        await Follow("/v1/account/two-factor/removal/confirm", request);

        var cancellation = await TakeOverToken(account.Login, TokenType.TwoFactorRemovalCancellation);
        (await Follow("/v1/account/two-factor/removal/cancel", cancellation))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.Username == account.Login);
        (await db.Set<UserTwoFactor>().FirstAsync(f => f.UserId == user.UserId))
            .RemovalDueUtc.Should().BeNull();
    }

    /// <summary>
    /// AC-23: a successful sign-in with the factor calls the removal off.
    /// </summary>
    /// <remarks>
    /// This is the path the real owner has: if the device is still in his hand,
    /// somebody else's request dies the moment he signs in.
    /// </remarks>
    [Fact]
    public async Task CallTheRemovalOffOnASuccessfulSignIn()
    {
        var account = await CreateUserWithFactor("tfmaillogin");

        await Client.PostAsJsonAsync("/v1/account/two-factor/removal", new { account.Email });
        var request = await TakeOverToken(account.Login, TokenType.TwoFactorRemovalRequest);
        await Follow("/v1/account/two-factor/removal/confirm", request);

        var login = await Client.PostAsJsonAsync(
            "/v1/account/login", new { account.Email, password = Password });
        var challenge = login.Headers.GetValues("Set-Cookie")
            .First(cookie => cookie.StartsWith("dm_2fa=", StringComparison.Ordinal))
            .Split(';')[0]["dm_2fa=".Length..];

        var finish = new HttpRequestMessage(HttpMethod.Post, "/v1/account/login/two-factor")
        {
            Content = JsonContent.Create(new
            {
                code = new Totp(Base32Encoding.ToBytes(account.Secret))
                    .ComputeTotp(DateTime.UtcNow.AddSeconds(30))
            })
        };
        finish.Headers.Add("Cookie", $"dm_2fa={challenge}");
        (await Client.SendAsync(finish)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.Username == account.Login);
        var state = await db.Set<UserTwoFactor>().FirstAsync(f => f.UserId == user.UserId);
        state.RemovalDueUtc.Should().BeNull(
            "signing in with the factor is the owner saying the device is still his");
        (await db.Set<SecurityAuditEntry>().CountAsync(e =>
                e.UserId == user.UserId &&
                e.EventType == DM.Domain.Account.Features.Security.SecurityEventType
                    .TwoFactorRemovalCancelled))
            .Should().Be(1);
    }

    /// <summary>A link followed twice opens once.</summary>
    [Fact]
    public async Task SpendALinkExactlyOnce()
    {
        var account = await CreateUserWithFactor("tfmailonce");

        await Client.PostAsJsonAsync("/v1/account/two-factor/removal", new { account.Email });
        var secret = await TakeOverToken(account.Login, TokenType.TwoFactorRemovalRequest);

        (await Follow("/v1/account/two-factor/removal/confirm", secret))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Follow("/v1/account/two-factor/removal/confirm", secret))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RefuseALinkNobodyIssued() =>
        (await Follow("/v1/account/two-factor/removal/confirm", Guid.NewGuid()))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
}
