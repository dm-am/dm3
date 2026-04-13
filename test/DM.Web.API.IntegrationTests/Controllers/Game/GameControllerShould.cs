using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// Integration tests for GameController
/// </summary>
public class GameControllerShould : IntegrationTestBase
{
    public GameControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
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
    /// Get own games without authentication should return OK with empty list
    /// </summary>
    [Fact]
    public async Task GetOwnGames_WhenNotAuthenticated_ReturnsEmptyList()
    {
        // Act - participating=true for anonymous returns empty list
        var response = await Client.GetAsync("/v1/games?participating=true");

        // Assert - API returns empty list for anonymous users
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region GetPopularGames Tests

    /// <summary>
    /// Get popular games should return OK
    /// </summary>
    [Fact]
    public async Task GetPopularGames_WithNoParameters_ReturnsOk()
    {
        // Act - Popular games use sortBy=popularity query parameter
        var response = await Client.GetAsync("/v1/games?sortBy=popularity&sortOrder=desc&take=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Search Tests

    /// <summary>
    /// Search games should return OK with results
    /// </summary>
    [Fact]
    public async Task GetGames_WithSearch_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/games?search=test");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Expected OK but got {response.StatusCode}. Content: {content}");
        }
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("resources");
    }

    /// <summary>
    /// Search games with explicit sort should respect sort (not use relevance)
    /// </summary>
    [Fact]
    public async Task GetGames_WithSearchAndSort_ReturnsOk()
    {
        // Act - search with explicit created sort (should not use relevance)
        var response = await Client.GetAsync("/v1/games?search=test&sortBy=created");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Expected OK but got {response.StatusCode}. Content: {content}");
        }
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Search games with pagination should return OK
    /// </summary>
    [Fact]
    public async Task GetGames_WithSearchAndPaging_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/games?search=test&size=5&number=1");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Expected OK but got {response.StatusCode}. Content: {content}");
        }
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Search games with status filter should return OK
    /// </summary>
    [Fact]
    public async Task GetGames_WithSearchAndStatusFilter_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/games?search=test&statuses=Active");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Expected OK but got {response.StatusCode}. Content: {content}");
        }
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Sorting Tests

    /// <summary>
    /// Sort by popularity should return OK
    /// </summary>
    [Fact]
    public async Task GetGames_SortByPopularity_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/games?sortBy=popularity&sortOrder=desc");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Expected OK but got {response.StatusCode}. Content: {content}");
        }
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("resources");
    }

    /// <summary>
    /// Sort by popularity ascending should return OK
    /// </summary>
    [Fact]
    public async Task GetGames_SortByPopularityAsc_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/games?sortBy=popularity&sortOrder=asc");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new Exception($"Expected OK but got {response.StatusCode}. Content: {content}");
        }
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("resources");
    }

    /// <summary>
    /// Sort by title should return OK
    /// </summary>
    [Fact]
    public async Task GetGames_SortByTitle_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/games?sortBy=title&sortOrder=asc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Sort by status should return OK
    /// </summary>
    [Fact]
    public async Task GetGames_SortByStatus_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/games?sortBy=status&sortOrder=asc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Sort by created date should return OK
    /// </summary>
    [Fact]
    public async Task GetGames_SortByCreated_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/games?sortBy=created&sortOrder=desc");

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
        content.Should().Contain("\"id\""); // Resource returned directly without wrapper
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
    /// GetGameDetails must return the game's active characters in the
    /// `activeCharacters` array — this feeds the game/room tooltip
    /// payload on the home page and Pulse. Regression guard for a
    /// silent mapping gap: the repository populated ActiveCharacters
    /// only on list paths, never on details, and the API-layer mapping
    /// relied on AutoMapper convention walking through a 3-level
    /// IncludeBase chain — both combined to silently drop the field.
    /// </summary>
    [Fact]
    public async Task GetGameDetails_IncludesActiveCharactersWithOwners()
    {
        var response = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}/details");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        // Seeded player characters must appear in the envelope's
        // activeCharacters array with their owner usernames.
        json.Should().Contain("activeCharacters");
        json.Should().Contain(TestConstants.TestCharacterName);
        json.Should().Contain(TestConstants.SecondCharacterName);
        json.Should().Contain(TestConstants.TestUserLogin);
        json.Should().Contain(TestConstants.SecondUserLogin);
    }

    /// <summary>
    /// GetGameDetails must carry the recruitment PcCount so the game
    /// tooltip renders "Персонажи: N/∞" with the real count instead of
    /// a misleading zero. Regression guard for the details endpoint
    /// previously skipping the EnrichGamesAsync helper that populates
    /// Recruitment.PcCount via a batched GROUP BY query.
    /// </summary>
    [Fact]
    public async Task GetGameDetails_CarriesRealPcCount()
    {
        var response = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}/details");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"pcCount\":2");
    }

    /// <summary>
    /// The rated posts endpoint must return `post.room.game` as a FULL
    /// sidebar-tier GameRef — master, assistants, activeCharacters,
    /// recruitment, subscribersCount — so GameLink / RoomLink on the
    /// home page and Pulse render exactly the same tooltip content
    /// that sidebar GameLink does. This is a STRUCTURAL assertion
    /// (JsonDocument traversal, not string.Contains) because a stray
    /// `"master":` at any level of the response would silently pass
    /// a substring match while the real tooltip pipeline still
    /// misses the nested field. Regression guard for the previous
    /// thin `{id, publicId, title}` projection on `post.room.game`.
    /// </summary>
    [Fact]
    public async Task GetRatedPosts_ReturnsFullGameRefOnPostRoomGame()
    {
        var response = await Client.GetAsync("/v1/posts?take=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var resources = doc.RootElement.GetProperty("resources");
        resources.GetArrayLength().Should().BeGreaterThan(0,
            "the seed includes one reviewed post in TestGame");

        var post = resources.EnumerateArray().First();
        var room = post.GetProperty("room");
        room.TryGetProperty("game", out var game).Should().BeTrue(
            "post.room.game is what GameLink / RoomLink read for tooltips");

        // Tooltip fields the sidebar primitive buildTooltip() reads.
        // These MUST be present and non-null on post.room.game, otherwise
        // the game tooltip on featured posts drifts from the sidebar one.
        game.TryGetProperty("master", out var master).Should().BeTrue();
        master.ValueKind.Should().Be(JsonValueKind.Object);
        master.GetProperty("username").GetString()
            .Should().Be(TestConstants.TestUserLogin);

        game.TryGetProperty("assistants", out _).Should().BeTrue();

        game.TryGetProperty("recruitment", out var recruitment).Should().BeTrue();
        recruitment.GetProperty("pcCount").GetInt32()
            .Should().Be(2, "two player characters are seeded in TestGame");

        game.TryGetProperty("subscribersCount", out _).Should().BeTrue();

        // Tooltip fields that feed the ROOM tooltip (buildRoomTooltip →
        // collectRoomParticipants reads game.activeCharacters for open
        // rooms, which is every featured / Pulse post). Must carry the
        // character NAMES with their OWNER USERNAMES — exactly what the
        // user-visible tooltip renders as "• Name (owner)".
        game.TryGetProperty("activeCharacters", out var activeChars).Should().BeTrue();
        activeChars.ValueKind.Should().Be(JsonValueKind.Array);
        activeChars.GetArrayLength().Should().Be(2);
        var names = activeChars.EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .ToArray();
        names.Should().Contain(TestConstants.TestCharacterName);
        names.Should().Contain(TestConstants.SecondCharacterName);
        var owners = activeChars.EnumerateArray()
            .Select(c => c.GetProperty("ownerUsername").GetString())
            .ToArray();
        owners.Should().Contain(TestConstants.TestUserLogin);
        owners.Should().Contain(TestConstants.SecondUserLogin);
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
        var response = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}/notepad");

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
        var response = await Client.GetAsync($"/v1/games/{nonExistentId}/notepad");

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
            username = "testuser"
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
            username = "testuser"
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
