using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
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

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", content);
        content.Should().Contain("resources");
    }

    /// <summary>
    /// Paging parameters should be echoed back in the paging envelope
    /// </summary>
    [Fact]
    public async Task GetGames_WithPaging_EchoesSkipAndTake()
    {
        // Act
        var response = await Client.GetAsync("/v1/games?skip=0&take=10");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", content);
        var paging = JsonDocument.Parse(content).RootElement.GetProperty("paging");
        paging.GetProperty("skip").GetInt32().Should().Be(0);
        paging.GetProperty("take").GetInt32().Should().Be(10);
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
        var content = await response.Content.ReadAsStringAsync();

        // Assert - API returns empty list for anonymous users
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", content);
        JsonDocument.Parse(content).RootElement
            .GetProperty("resources").EnumerateArray().Should().BeEmpty();
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
    /// Get non-existent game should return NotFound. The game endpoints answer
    /// the same code for an id that never existed and for one this reader may
    /// not see, and neither of those is "somebody deleted it".
    /// </summary>
    [Fact]
    public async Task GetGame_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/games/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
        // Scoped to the seeded game. Unscoped, this took the first row of a
        // global rating-ordered list and assumed it was the seed's — true only
        // while nothing else in the database had a rated post, which is not a
        // property of the endpoint under test.
        var response = await Client.GetAsync($"/v1/posts?gameId={TestConstants.TestGameId}&take=10");
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
    /// One endpoint, two ways to address the game, one payload.
    /// </summary>
    /// <remarks>
    /// GET /v1/games/{id} accepts either the five-letter alias or the GUID and
    /// takes a different repository path for each. The alias path skipped the
    /// enrichment step, so it answered subscribersCount 0, no active characters
    /// and pcCount 0 for a game whose GUID form reported all three — and it
    /// decided GameIntention.Read on an empty pending-invitation set.
    ///
    /// Asserted as an equality between the two responses rather than against
    /// fixed numbers: the point is that the address cannot change the answer,
    /// and fixed numbers would drift with the seed.
    /// </remarks>
    [Fact]
    public async Task GetGame_AnswerTheSameWhetherAddressedByAliasOrByGuid()
    {
        var byGuid = await ReadGameAsync($"/v1/games/{TestConstants.TestGameId}");
        var byAlias = await ReadGameAsync($"/v1/games/{TestConstants.TestGamePublicId}");

        byAlias.GetProperty("id").GetString()
            .Should().Be(byGuid.GetProperty("id").GetString(),
                "the two addresses must resolve to the same game");

        foreach (var field in new[] { "subscribersCount", "gameReviewsCount", "postReviewsCount" })
        {
            byAlias.GetProperty(field).GetInt32()
                .Should().Be(byGuid.GetProperty(field).GetInt32(), field);
        }

        byAlias.GetProperty("activeCharacters").GetArrayLength()
            .Should().Be(byGuid.GetProperty("activeCharacters").GetArrayLength());
        byAlias.GetProperty("recruitment").GetProperty("pcCount").GetInt32()
            .Should().Be(byGuid.GetProperty("recruitment").GetProperty("pcCount").GetInt32());
    }

    /// <summary>The `resource` of a single-game read, as a JSON element.</summary>
    private async Task<JsonElement> ReadGameAsync(string url)
    {
        var response = await Client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, url);

        // Parsed into a document that outlives the using: cloning is what makes
        // the element safe to read after the reader is disposed.
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("resource").Clone();
    }

    /// <summary>
    /// The reader bit on `post.room.game.participation`, over the wire.
    /// </summary>
    /// <remarks>
    /// A separate test from the structural one above rather than an assertion
    /// added to it: that one is deliberately anonymous, and this bit is the one
    /// field on the attached game that depends on who is asking.
    ///
    /// It guards the whole chain and nothing less than the whole chain: the feed
    /// loads the attached game anonymously — its accessibility is fixed by the
    /// feed's own filters — and fills the viewer's subscription separately, then
    /// GameParticipationResolver turns that flag into the Reader bit and the
    /// serializer writes it out. A repository test covers the flag
    /// (PostRepositoryShould), so what is new here is the last two steps.
    ///
    /// "Reader" is capitalised because JsonConfiguration registers
    /// JsonStringEnumConverter with no naming policy: the camelCase policy next
    /// to it applies to property names, not to enum values. The client mirrors
    /// the string literally, so the casing is the contract.
    /// </remarks>
    [Fact]
    public async Task GetRatedPosts_ReportsTheViewerAsAReaderOfTheGameTheySubscribeTo()
    {
        // No arrange: the fixture already subscribes InactiveUser1 to TestGame.
        var subscriber = new GeneralUser
        {
            UserId = TestConstants.InactiveUser1Id,
            Username = TestConstants.InactiveUser1Username,
            Role = UserRole.RegularUser,
            AccessPolicy = AccessPolicy.NotSpecified,
        };

        var participation = await ReadRatedPostParticipationAsync(
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                $"/v1/posts?gameId={TestConstants.TestGameId}&take=10",
                subscriber));

        participation.Should().Contain("Reader");
    }

    /// <summary>
    /// The same read for somebody who subscribes to nothing. Without it the
    /// assertion above passes on a resolver that returns every bit for everyone.
    /// </summary>
    [Fact]
    public async Task GetRatedPosts_DoesNotReportAStrangerAsAReader()
    {
        var stranger = new GeneralUser
        {
            UserId = TestConstants.InactiveUser2Id,
            Username = TestConstants.InactiveUser2Username,
            Role = UserRole.RegularUser,
            AccessPolicy = AccessPolicy.NotSpecified,
        };

        var participation = await ReadRatedPostParticipationAsync(
            CreateAuthenticatedRequest(
                HttpMethod.Get,
                $"/v1/posts?gameId={TestConstants.TestGameId}&take=10",
                stranger));

        // InactiveUser2 subscribes to SecondGame, not to this one — so the read
        // also shows the flag is filled per game and not per subscriber.
        participation.Should().NotContain("Reader");
    }

    /// <summary>
    /// `participation` off the first rated post of the seeded game, as strings.
    /// </summary>
    private async Task<string[]> ReadRatedPostParticipationAsync(HttpRequestMessage request)
    {
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var resources = doc.RootElement.GetProperty("resources");
        resources.GetArrayLength().Should().BeGreaterThan(0,
            "the seed includes one reviewed post in TestGame");

        var game = resources.EnumerateArray().First()
            .GetProperty("room")
            .GetProperty("game");

        return game.GetProperty("participation")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
    }

    /// <summary>
    /// Get non-existent game details should return NotFound
    /// </summary>
    [Fact]
    public async Task GetGameDetails_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/games/{nonExistentId}/details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
    /// Get readers of non-existent game should return NotFound
    /// </summary>
    [Fact]
    public async Task GetReaders_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/games/{nonExistentId}/readers");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
