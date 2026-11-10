using System.Net;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Messaging;

public class GlobalChatControllerShould : IntegrationTestBase
{
    public GlobalChatControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetGlobalChatEventsWhenNotAuthenticatedReturnsOk()
    {
        var response = await Client.GetAsync("/v1/global-chat/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

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
