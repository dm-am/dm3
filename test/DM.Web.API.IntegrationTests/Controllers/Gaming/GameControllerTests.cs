using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Gaming;

/// <summary>
/// Integration tests for GameController
/// </summary>
public class GameControllerTests : IntegrationTestBase
{
    public GameControllerTests(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    #region GetGames Tests

    /// <summary>
    /// Get list of games should return OK
    /// </summary>
    [Fact]
    public async Task GetGames_WithNoParameters_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/games");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - log content if not OK for debugging
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Expected OK but got {response.StatusCode}. Content: {content}");
        }
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("resources");
    }

    /// <summary>
    /// Get list of games with query parameters should return OK
    /// </summary>
    [Fact]
    public async Task GetGames_WithQueryParameters_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/games?size=10&number=1");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - log content if not OK for debugging
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Expected OK but got {response.StatusCode}. Content: {content}");
        }
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region GetOwnGames Tests

    /// <summary>
    /// Get own games without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task GetOwnGames_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/v1/games/owned");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetPopularGames Tests

    /// <summary>
    /// Get popular games should return OK
    /// </summary>
    [Fact]
    public async Task GetPopularGames_WithNoParameters_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/games/popular");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region GetTags Tests

    /// <summary>
    /// Get game tags should return OK
    /// </summary>
    [Fact]
    public async Task GetTags_WithNoParameters_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/games/tags");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region GetGame Tests

    /// <summary>
    /// Get existing game should return OK
    /// </summary>
    [Fact]
    public async Task GetGame_WithValidId_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("resource");
    }

    /// <summary>
    /// Get non-existent game should return Gone
    /// </summary>
    [Fact]
    public async Task GetGame_WithNonExistentId_ReturnsGone()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/games/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    #endregion

    #region GetGameDetails Tests

    /// <summary>
    /// Get existing game details should return OK
    /// </summary>
    [Fact]
    public async Task GetGameDetails_WithValidId_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}/details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Get non-existent game details should return Gone
    /// </summary>
    [Fact]
    public async Task GetGameDetails_WithNonExistentId_ReturnsGone()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/games/{nonExistentId}/details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    #endregion

    #region GetGameNotes Tests

    /// <summary>
    /// Get game notes without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task GetGameNotes_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}/notes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Get notes for non-existent game should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task GetGameNotes_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/games/{nonExistentId}/notes");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region PostGame Tests

    /// <summary>
    /// Create game without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task PostGame_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var game = new
        {
            title = "New Test Game",
            systemName = "D&D",
            narrativeSetting = "Test Setting",
            info = "Test game info"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/games", game);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region PatchGame Tests

    /// <summary>
    /// Update game without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task PatchGame_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var gameUpdate = new
        {
            title = "Updated Title"
        };
        var content = JsonContent.Create(gameUpdate);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/games/{TestConstants.TestGameId}/details")
        {
            Content = content
        };

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Update non-existent game should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task PatchGame_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var gameUpdate = new
        {
            title = "Updated Title"
        };
        var content = JsonContent.Create(gameUpdate);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/games/{nonExistentId}/details")
        {
            Content = content
        };

        // Act
        var response = await Client.SendAsync(request);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region PatchGameNotes Tests

    /// <summary>
    /// Update game notes without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task PatchGameNotes_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var notes = new
        {
            notepad = "Updated notes"
        };
        var content = JsonContent.Create(notes);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/games/{TestConstants.TestGameId}/notes")
        {
            Content = content
        };

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Update notes for non-existent game should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task PatchGameNotes_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var notes = new
        {
            notepad = "Updated notes"
        };
        var content = JsonContent.Create(notes);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/games/{nonExistentId}/notes")
        {
            Content = content
        };

        // Act
        var response = await Client.SendAsync(request);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region DeleteGame Tests

    /// <summary>
    /// Delete game without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task DeleteGame_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.DeleteAsync($"/v1/games/{TestConstants.TestGameId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Delete non-existent game should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task DeleteGame_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/games/{nonExistentId}");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetReaders Tests

    /// <summary>
    /// Get readers of existing game should return OK
    /// </summary>
    [Fact]
    public async Task GetReaders_WithValidId_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}/readers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Get readers of non-existent game should return Gone
    /// </summary>
    [Fact]
    public async Task GetReaders_WithNonExistentId_ReturnsGone()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/games/{nonExistentId}/readers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    #endregion

    #region PostReader Tests

    /// <summary>
    /// Subscribe to game without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task PostReader_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.PostAsync($"/v1/games/{TestConstants.TestGameId}/readers", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Subscribe to non-existent game should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task PostReader_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsync($"/v1/games/{nonExistentId}/readers", null);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region DeleteReader Tests

    /// <summary>
    /// Unsubscribe from game without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task DeleteReader_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.DeleteAsync($"/v1/games/{TestConstants.TestGameId}/readers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Unsubscribe from non-existent game should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task DeleteReader_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/games/{nonExistentId}/readers");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetBlacklist Tests

    /// <summary>
    /// Get game blacklist without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task GetBlacklist_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}/blacklist");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Get blacklist for non-existent game should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task GetBlacklist_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/games/{nonExistentId}/blacklist");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region PostBlacklist Tests

    /// <summary>
    /// Add user to blacklist without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task PostBlacklist_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var user = new
        {
            login = "testuser"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/v1/games/{TestConstants.TestGameId}/blacklist", user);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Add user to blacklist for non-existent game should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task PostBlacklist_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var user = new
        {
            login = "testuser"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/v1/games/{nonExistentId}/blacklist", user);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region DeleteBlacklist Tests

    /// <summary>
    /// Remove user from blacklist without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task DeleteBlacklist_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.DeleteAsync($"/v1/games/{TestConstants.TestGameId}/blacklist/testuser");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Remove user from blacklist for non-existent game should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task DeleteBlacklist_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/games/{nonExistentId}/blacklist/testuser");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region ReadGameComments Tests

    /// <summary>
    /// Mark game comments as read without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task ReadGameComments_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.DeleteAsync($"/v1/games/{TestConstants.TestGameId}/comments/unread");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Mark comments as read for non-existent game should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task ReadGameComments_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/games/{nonExistentId}/comments/unread");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region ReadGameCharacters Tests

    /// <summary>
    /// Mark game characters as read without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task ReadGameCharacters_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.DeleteAsync($"/v1/games/{TestConstants.TestGameId}/characters/unread");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Mark characters as read for non-existent game should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task ReadGameCharacters_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/games/{nonExistentId}/characters/unread");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
