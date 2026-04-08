using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Community;

/// <summary>
/// Integration tests for PollController
/// </summary>
public class PollControllerShould : IntegrationTestBase
{
    public PollControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetPolls_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/polls");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostPoll_RequiresAuthentication()
    {
        // Arrange
        var pollData = new
        {
            title = "Test Poll",
            options = new[] { "Option 1", "Option 2" }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/polls", pollData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostPoll_WithAuth_RequiresSeniorModeratorRole()
    {
        // Arrange
        var pollData = new
        {
            title = "Test Poll",
            options = new[] { "Option 1", "Option 2" }
        };
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/v1/polls");
        request.Content = JsonContent.Create(pollData);

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Regular user should be forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostPollVote_RequiresAuthentication()
    {
        // Arrange
        var pollId = Guid.NewGuid();
        var optionId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsync($"/v1/polls/{pollId}/vote?optionId={optionId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletePollVote_RequiresAuthentication()
    {
        // Arrange
        var pollId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/polls/{pollId}/vote");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletePoll_RequiresAuthentication()
    {
        // Arrange
        var pollId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/polls/{pollId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
