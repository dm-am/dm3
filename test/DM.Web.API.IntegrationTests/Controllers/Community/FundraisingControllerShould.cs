using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Community;

/// <summary>
/// Integration tests for FundraisingController
/// </summary>
public class FundraisingControllerShould : IntegrationTestBase
{
    private const decimal SeededGoalAmount = 50000m;
    private const decimal SeededCollectedAmount = 17000m;

    public FundraisingControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetFundraising_ReturnsOk_WithSeededValues()
    {
        // Act - available anonymously
        var response = await Client.GetAsync("/v1/fundraising");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var resource = doc.RootElement.GetProperty("resource");
        resource.GetProperty("goalAmount").GetDecimal().Should().Be(SeededGoalAmount);
        resource.GetProperty("collectedAmount").GetDecimal().Should().Be(SeededCollectedAmount);
    }

    [Fact]
    public async Task PutFundraising_RequiresAuthentication()
    {
        // Arrange
        var fundraisingData = new
        {
            goalAmount = 60000,
            collectedAmount = 20000
        };

        // Act
        var response = await Client.PutAsJsonAsync("/v1/fundraising", fundraisingData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutFundraising_WithAuth_RequiresAdminRole()
    {
        // Arrange
        var fundraisingData = new
        {
            goalAmount = 60000,
            collectedAmount = 20000
        };
        var request = CreateAuthenticatedRequest(HttpMethod.Put, "/v1/fundraising");
        request.Content = JsonContent.Create(fundraisingData);

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Regular user should be forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutFundraising_AsAdmin_UpdatesAndPersistsValues()
    {
        // Arrange
        var fundraisingData = new
        {
            goalAmount = 75000,
            collectedAmount = 31000
        };
        var request = CreateAdminRequest(HttpMethod.Put, "/v1/fundraising");
        request.Content = JsonContent.Create(fundraisingData);

        try
        {
            // Act
            var response = await Client.SendAsync(request);

            // Assert - response carries the updated values
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = await response.Content.ReadAsStringAsync();
            using (var doc = JsonDocument.Parse(json))
            {
                var resource = doc.RootElement.GetProperty("resource");
                resource.GetProperty("goalAmount").GetDecimal().Should().Be(75000m);
                resource.GetProperty("collectedAmount").GetDecimal().Should().Be(31000m);
            }

            // Assert - values are persisted (visible to a subsequent anonymous GET)
            var getResponse = await Client.GetAsync("/v1/fundraising");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var getJson = await getResponse.Content.ReadAsStringAsync();
            using (var getDoc = JsonDocument.Parse(getJson))
            {
                var resource = getDoc.RootElement.GetProperty("resource");
                resource.GetProperty("goalAmount").GetDecimal().Should().Be(75000m);
                resource.GetProperty("collectedAmount").GetDecimal().Should().Be(31000m);
            }
        }
        finally
        {
            // Restore the seeded values: the table is single-row and shared
            // by all tests in the collection, so the seeded-values GET test
            // must not depend on execution order
            var restoreRequest = CreateAdminRequest(HttpMethod.Put, "/v1/fundraising");
            restoreRequest.Content = JsonContent.Create(new
            {
                goalAmount = SeededGoalAmount,
                collectedAmount = SeededCollectedAmount
            });
            var restoreResponse = await Client.SendAsync(restoreRequest);
            restoreResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task PutFundraising_AsAdmin_WithZeroGoalAmount_ReturnsBadRequest()
    {
        // Arrange
        var fundraisingData = new
        {
            goalAmount = 0,
            collectedAmount = 1000
        };
        var request = CreateAdminRequest(HttpMethod.Put, "/v1/fundraising");
        request.Content = JsonContent.Create(fundraisingData);

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutFundraising_AsAdmin_WithNegativeCollectedAmount_ReturnsBadRequest()
    {
        // Arrange
        var fundraisingData = new
        {
            goalAmount = 50000,
            collectedAmount = -1
        };
        var request = CreateAdminRequest(HttpMethod.Put, "/v1/fundraising");
        request.Content = JsonContent.Create(fundraisingData);

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
