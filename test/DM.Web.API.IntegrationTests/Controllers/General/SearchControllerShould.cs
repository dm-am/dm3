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
    public async Task Search_WhenEngineUnreachable_DegradesToServiceUnavailable()
    {
        // Act
        var response = await Client.GetAsync("/v1/search?query=test");

        // Assert - the fixture runs no search worker, so this covers graceful degradation
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
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
    public async Task Search_WithPaging_WhenEngineUnreachable_DegradesToServiceUnavailable()
    {
        // Act
        var response = await Client.GetAsync("/v1/search?query=test&skip=0&take=10");

        // Assert - the fixture runs no search worker, so this covers graceful degradation
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
