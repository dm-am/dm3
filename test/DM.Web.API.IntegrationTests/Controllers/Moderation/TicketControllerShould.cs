using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    public async Task GetMyFiledTickets_TakesThePageAndReportsTheTotal()
    {
        // Both lists used to answer with everything and say nothing about it.
        // The parameters have to bind on the wire, not only in the signature,
        // and the envelope has to carry the total: a truncated answer that does
        // not say how much it truncated leaves the caller unable to ask for the
        // rest.
        var request = CreateAuthenticatedRequest(
            HttpMethod.Get, "/v1/moderation/tickets/mine?skip=0&take=1");

        var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);
        var paging = JsonDocument.Parse(body).RootElement.GetProperty("paging");
        paging.GetProperty("take").GetInt32().Should().Be(1);
        paging.TryGetProperty("total", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetMyAssignedTickets_TakesThePageAndReportsTheTotal()
    {
        // The moderator queue: same contract, and it is reached with a role that
        // passes the moderator gate.
        var request = CreateAdminRequest(
            HttpMethod.Get, "/v1/moderation/tickets/assigned?skip=0&take=1");

        var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);
        var paging = JsonDocument.Parse(body).RootElement.GetProperty("paging");
        paging.GetProperty("take").GetInt32().Should().Be(1);
        paging.TryGetProperty("total", out _).Should().BeTrue();
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
