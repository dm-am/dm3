using System.Net;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// Integration tests for SearchController
/// </summary>
public class SearchControllerShould : IntegrationTestBase
{
    public SearchControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task Search_WithValidQuery_ReturnsOkOrServiceUnavailable()
    {
        // Act
        var response = await Client.GetAsync("/v1/search?query=test");

        // Assert - Search service may not be available in integration tests
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Search_WithoutQuery_ReturnsBadRequest()
    {
        // Act
        var response = await Client.GetAsync("/v1/search");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_WithEmptyQuery_ReturnsBadRequest()
    {
        // Act
        var response = await Client.GetAsync("/v1/search?query=");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_WithPagination_ReturnsOkOrServiceUnavailable()
    {
        // Act
        var response = await Client.GetAsync("/v1/search?query=test&skip=0&take=10");

        // Assert - Search service may not be available in integration tests
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.ServiceUnavailable);
    }
}
