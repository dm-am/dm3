using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

/// <summary>
/// Integration tests for TicketController
/// </summary>
public class TicketControllerShould : IntegrationTestBase
{
    public TicketControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetTickets_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/moderation/tickets");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTickets_WithAuth_RequiresModeratorRole()
    {
        // Arrange
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/moderation/tickets");

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Regular user should be forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTicketStats_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/moderation/tickets/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyAssignedTickets_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/moderation/tickets/assigned");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyFiledTickets_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/moderation/tickets/mine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateTicket_RequiresAuthentication()
    {
        // Arrange
        var ticketData = new
        {
            targetUserLogin = TestConstants.SecondUserLogin,
            reason = "Spam",
            description = "Test report"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/moderation/tickets", ticketData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssignTicketToMe_RequiresAuthentication()
    {
        // Arrange
        var ticketId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsync($"/v1/moderation/tickets/{ticketId}/assign", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ResolveTicket_RequiresAuthentication()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var resolveData = new { resolution = "Resolved" };

        // Act
        var response = await Client.PostAsJsonAsync($"/v1/moderation/tickets/{ticketId}/resolve", resolveData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
