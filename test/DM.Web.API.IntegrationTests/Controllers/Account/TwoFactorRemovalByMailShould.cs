using System.Net;
using System.Net.Http.Json;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Tokens;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using OtpNet;
using DM.Web.API.Shared.Http;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Account;

/// <summary>
/// The mailed way back in, for somebody who has lost the device and the codes.
/// </summary>
/// <remarks>
/// The link schedules the removal a week out, ends every session and hands back a way to call it off; signing
/// in with the factor calls it off too.
/// </remarks>
public class TwoFactorRemovalByMailShould : IntegrationTestBase
{
    public TwoFactorRemovalByMailShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private sealed record Account(string Login, string Email, string Session, string Secret);

    private async Task<Account> CreateUserWithFactor(string prefix)
    {
        var (login, email) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, prefix, TestConstants.Password);
        var session = await UserTestHelper.Login(Client, email, TestConstants.Password);

        var (secret, _) = await UserTestHelper.EnableSecondFactor(Client, session, TestConstants.Password);

        return new Account(login, email, session, secret);
    }

    /// <summary>
    /// Stamps a known secret onto the live token of a type and returns it.
    /// </summary>
    /// <remarks>The row keeps only a hash, so the test issues a value of its own and stamps its hash.</remarks>
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
        request.Headers.Add(TokenHeaders.Account, secret.ToString());
        return Client.SendAsync(request);
    }

    /// <summary>
    /// The link schedules, it does not perform, and it ends the sessions.
    /// </summary>
    [Fact]
    public async Task ScheduleTheRemovalAndEndEverySession()
    {
        var account = await CreateUserWithFactor("tfmailsch");

        (await Client.PostAsJsonAsync("/v1/account/two-factor/removal", new { account.Email }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

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
    /// A successful sign-in with the factor calls the removal off.
    /// </summary>
    /// <remarks>The real owner, if the device is still in his hand, ends the request by signing in.</remarks>
    [Fact]
    public async Task CallTheRemovalOffOnASuccessfulSignIn()
    {
        var account = await CreateUserWithFactor("tfmaillogin");

        await Client.PostAsJsonAsync("/v1/account/two-factor/removal", new { account.Email });
        var request = await TakeOverToken(account.Login, TokenType.TwoFactorRemovalRequest);
        await Follow("/v1/account/two-factor/removal/confirm", request);

        var login = await Client.PostAsJsonAsync(
            "/v1/account/login", new { account.Email, password = TestConstants.Password });
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
