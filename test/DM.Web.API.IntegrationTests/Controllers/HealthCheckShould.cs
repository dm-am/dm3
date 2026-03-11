using System.Net;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for health endpoints
/// </summary>
public class HealthCheckShould : IntegrationTestBase
{
    public HealthCheckShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Health check endpoint should return healthy status
    /// </summary>
    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Act
        var response = await Client.GetAsync("/_health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Swagger endpoint should be available (using Forum group as example)
    /// </summary>
    [Fact]
    public async Task Swagger_ReturnsOk()
    {
        // Act - Swagger docs are generated per API group (Forum, Game, etc.)
        var response = await Client.GetAsync("/swagger/Forum/swagger.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("openapi");
    }
}
