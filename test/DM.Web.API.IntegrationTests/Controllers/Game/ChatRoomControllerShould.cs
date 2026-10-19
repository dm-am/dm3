using System.Net;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

public class ChatRoomControllerShould : IntegrationTestBase
{
    public ChatRoomControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Get chat rooms without authentication should return OK (public endpoint)
    /// </summary>
    [Fact]
    public async Task GetChatRoomsWhenNotAuthenticatedReturnsOk()
    {
        var response = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}/chat-rooms");

        // Public endpoint
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetChatRoomWithNonExistentIdReturnsNotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await Client.GetAsync($"/v1/chat-rooms/{nonExistentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Chat room messages are read without an account, so a guest asking for a missing room is told it is missing.
    /// </summary>
    [Fact]
    public async Task GetChatRoomMessagesWhenNotAuthenticatedWithNonExistentIdReturnsNotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await Client.GetAsync($"/v1/chat-rooms/{nonExistentId}/messages");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

}
