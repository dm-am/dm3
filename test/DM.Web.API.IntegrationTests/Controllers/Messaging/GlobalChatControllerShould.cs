using System.Net;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Messaging;

public class GlobalChatControllerShould : IntegrationTestBase
{
    public GlobalChatControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Get global chat events should return OK (public endpoint)
    /// </summary>
    [Fact]
    public async Task GetGlobalChatEventsWhenNotAuthenticatedReturnsOk()
    {
        var response = await Client.GetAsync("/v1/global-chat/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Get active global chat event should return OK or NoContent (depending on whether an event is active)
    /// </summary>
    [Fact]
    public async Task GetActiveGlobalChatEventWhenNoneActiveReturnsNoContent()
    {
        var response = await Client.GetAsync("/v1/global-chat/events/active");

        // Nothing in the fixture opens an event
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetGlobalChatEventWithNonExistentIdReturnsNotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await Client.GetAsync($"/v1/global-chat/events/{nonExistentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

}
