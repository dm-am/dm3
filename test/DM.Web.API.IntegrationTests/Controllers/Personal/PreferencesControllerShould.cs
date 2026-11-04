using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbUserContact = DM.Infrastructure.Persistence.Entities.Account.UserContact;

namespace DM.Web.API.IntegrationTests.Controllers.Personal;

public class PreferencesControllerShould : IntegrationTestBase
{
    public PreferencesControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetPreferencesRequiresAuthentication()
    {
        var response = await Client.GetAsync("/v1/users/me/preferences");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPreferencesWithAuthReturnsOk()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/users/me/preferences");

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A change of preferences leaves the contacts of the profile as they are.
    /// </summary>
    /// <remarks>Preferences and the profile share one update of the user, which must not read absent contacts as none.</remarks>
    [Fact]
    public async Task KeepTheContactsOfTheProfileWhenPreferencesChange()
    {
        var user = await SeedUserWithContact("Telegram", "@reader");

        var request = CreateAuthenticatedRequest(HttpMethod.Patch, "/v1/users/me/preferences", user);
        request.Content = JsonContent.Create(new { pageSize = 20 });
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        await using var db = DatabaseFixture.CreateDbContext();
        var contacts = await db.UserContacts.Where(c => c.UserId == user.UserId).ToListAsync();
        contacts.Should().ContainSingle("preferences say nothing of contacts")
            .Which.ContactValue.Should().Be("@reader");
    }

    /// <summary>
    /// The participation in the rating is a visibility setting of the profile:
    /// saved by the profile, onto the account, and read back by it. The
    /// preferences neither carry it nor touch it.
    /// </summary>
    [Fact]
    public async Task SaveTheParticipationInTheRatingByTheProfile()
    {
        var user = await SeedUserWithContact("Telegram", "@reader");

        var patch = CreateAuthenticatedRequest(HttpMethod.Patch, "/v1/users/me/profile", user);
        patch.Content = JsonContent.Create(new { visibility = new { showRating = false } });
        var response = await Client.SendAsync(patch);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            (await db.Users.SingleAsync(u => u.UserId == user.UserId)).RatingDisabled.Should().BeTrue();
        }

        var profile = await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, "/v1/users/me/profile", user));
        using (var read = JsonDocument.Parse(await profile.Content.ReadAsStringAsync()))
        {
            read.RootElement.GetProperty("visibility").GetProperty("showRating").GetBoolean().Should().BeFalse();
        }

        var preferencesPatch = CreateAuthenticatedRequest(HttpMethod.Patch, "/v1/users/me/preferences", user);
        preferencesPatch.Content = JsonContent.Create(new { pageSize = 50 });
        var preferencesResponse = await Client.SendAsync(preferencesPatch);
        using var preferences = JsonDocument.Parse(await preferencesResponse.Content.ReadAsStringAsync());
        preferences.RootElement.TryGetProperty("ratingEnabled", out _).Should().BeFalse();

        await using var after = DatabaseFixture.CreateDbContext();
        (await after.Users.SingleAsync(u => u.UserId == user.UserId)).RatingDisabled.Should().BeTrue(
            "a change of preferences leaves the participation in the rating as it is");
    }

    /// <summary>
    /// A page size the account form does not offer is refused under the field.
    /// </summary>
    [Fact]
    public async Task RefuseAPageSizeTheFormDoesNotOffer()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Patch, "/v1/users/me/preferences");
        request.Content = JsonContent.Create(new { pageSize = 15 });
        var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, body);
        body.Should().Contain("Недопустимое число на странице");
    }

    private async Task<GeneralUser> SeedUserWithContact(string type, string value)
    {
        var userId = Guid.NewGuid();
        // Username is varchar(20) and uniquely indexed, so the id is truncated
        // rather than used whole.
        var username = $"pc{userId:N}"[..20];

        await using var db = DatabaseFixture.CreateDbContext();
        db.Users.Add(new DbUser
        {
            UserId = userId,
            Username = username,
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
            LastActivityUtc = DateTimeOffset.UtcNow
        });
        db.UserContacts.Add(new DbUserContact
        {
            UserContactId = Guid.NewGuid(),
            UserId = userId,
            ContactType = type,
            ContactValue = value,
            SortOrder = 0
        });
        await db.SaveChangesAsync();

        return new GeneralUser
        {
            UserId = userId,
            Username = username,
            Role = UserRole.RegularUser,
            AccessPolicy = AccessPolicy.NotSpecified
        };
    }
}
