using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using DM.Domain.Core.Tokens;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Web.API.Shared.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Account;

/// <summary>
/// An account starts in the time zone of the device it was created on.
/// </summary>
/// <remarks>
/// The site draws a date in the reader's browser, and a signed-in reader's letters
/// are stamped in the zone of the account, so an account that started in UTC sent its
/// first letters hours off until its owner found the setting. The request that creates
/// the account - the activation, where the login is chosen - carries the zone of the
/// device and the account keeps it. A zone the server cannot print a time in is left
/// out, and the account is created all the same: a zone is no reason to refuse one.
/// </remarks>
public class RegistrationTimeZoneShould : IntegrationTestBase
{
    private const string Password = "TestPass123ok";

    public RegistrationTimeZoneShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task KeepTheZoneOfTheDeviceTheAccountWasCreatedOn()
    {
        var userId = await RegisterAsync("Europe/Moscow");

        (await ZoneOfAsync(userId)).Should().Be("Europe/Moscow");
    }

    [Theory]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateTheAccountInUtcWhenTheZoneIsNoZone(string? zone)
    {
        var userId = await RegisterAsync(zone);

        (await ZoneOfAsync(userId)).Should().BeNull(
            "a name no zone answers to is left out, and the account keeps UTC");
    }

    /// <summary>Registers, then activates with the zone given, and answers the new account's id.</summary>
    private async Task<Guid> RegisterAsync(string? zone)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var email = $"zone{suffix}@test.example.com";
        var login = $"zone{suffix}";

        var register = await Client.PostAsJsonAsync("/v1/account/register",
            new { email, password = Password, acceptedRules = true });
        register.StatusCode.Should().Be(HttpStatusCode.Created, await register.Content.ReadAsStringAsync());

        // The pending row keeps only a hash of the secret in the letter, so a
        // secret of the test's own is stamped onto it, as UserTestHelper does.
        var secret = Guid.NewGuid();
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            var pending = await db.Set<PendingRegistration>().SingleAsync(p => p.Email == email);
            pending.SecretHash = ConfirmationSecret.Hash(secret);
            await db.SaveChangesAsync();
        }

        var activate = new HttpRequestMessage(HttpMethod.Post, "/v1/account/activation")
        {
            Content = JsonContent.Create(new { username = login, timeZoneId = zone })
        };
        activate.Headers.Add(TokenHeaders.Account, secret.ToString());
        var activated = await Client.SendAsync(activate);
        activated.StatusCode.Should().Be(HttpStatusCode.OK,
            "the account is created whatever zone came with it; body was: {0}",
            await activated.Content.ReadAsStringAsync());

        await using var check = DatabaseFixture.CreateDbContext();
        return await check.Users.Where(u => u.Username == login).Select(u => u.UserId).SingleAsync();
    }

    private async Task<string?> ZoneOfAsync(Guid userId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        return await db.UserSettings
            .Where(s => s.UserId == userId)
            .Select(s => s.TimeZoneId)
            .SingleOrDefaultAsync();
    }
}
