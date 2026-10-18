using System.Net;
using System.Net.Http.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbUserContact = DM.Infrastructure.Persistence.Entities.Account.UserContact;

namespace DM.Web.API.IntegrationTests.Controllers.Personal;

/// <summary>
/// Integration tests for PreferencesController
/// </summary>
public class PreferencesControllerShould : IntegrationTestBase
{
    public PreferencesControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetPreferences_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/users/me/preferences");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPreferences_WithAuth_ReturnsOk()
    {
        // Arrange
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/users/me/preferences");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdatePreferences_RequiresAuthentication()
    {
        // Arrange
        var preferencesData = new
        {
            paginationSize = 25,
            colorSchema = "dark"
        };

        // Act
        var response = await Client.PatchAsJsonAsync("/v1/users/me/preferences", preferencesData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A change of preferences leaves the contacts of the profile as they are.
    /// </summary>
    /// <remarks>
    /// Preferences are written by the same update of the user as the profile,
    /// and that update reads contacts it was given no list of as "no contacts
    /// at all" if its default says so: every change of a theme, a page size or
    /// a time zone then deleted the reader's contacts, and one meeting a save
    /// of the profile answered 500 when both deleted the same rows.
    /// </remarks>
    [Fact]
    public async Task KeepTheContactsOfTheProfileWhenPreferencesChange()
    {
        var user = await SeedUserWithContact("Telegram", "@reader");

        var request = CreateAuthenticatedRequest(HttpMethod.Patch, "/v1/users/me/preferences", user);
        request.Content = JsonContent.Create(new { paging = new { postsPerPage = 20 } });
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        await using var db = DatabaseFixture.CreateDbContext();
        var contacts = await db.UserContacts.Where(c => c.UserId == user.UserId).ToListAsync();
        contacts.Should().ContainSingle("preferences say nothing of contacts")
            .Which.ContactValue.Should().Be("@reader");
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
