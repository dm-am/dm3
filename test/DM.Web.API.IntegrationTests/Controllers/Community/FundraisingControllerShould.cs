using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Community;

/// <summary>
/// Integration tests for FundraisingController
/// </summary>
public class FundraisingControllerShould : IntegrationTestBase
{
    // The single seeded row, field for field (DmDbContext, FundraisingGoal
    // region). The title belongs here as much as the amounts do: the goal is a
    // whole resource that PUT replaces, so a restore that omits it either fails
    // validation or blanks a field the next test reads.
    private const string SeededTitle = "Хостинг и домен на год";
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
        resource.GetProperty("title").GetString().Should().Be(SeededTitle);
        resource.GetProperty("goalAmount").GetDecimal().Should().Be(SeededGoalAmount);
        resource.GetProperty("collectedAmount").GetDecimal().Should().Be(SeededCollectedAmount);
    }

    [Fact]
    public async Task PutFundraising_RequiresAuthentication()
    {
        // Arrange
        var fundraisingData = new
        {
            title = "Новый сервер",
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
            title = "Новый сервер",
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
        const string updatedTitle = "Переезд на новый хостинг";
        var fundraisingData = new
        {
            title = updatedTitle,
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
                resource.GetProperty("title").GetString().Should().Be(updatedTitle);
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
                resource.GetProperty("title").GetString().Should().Be(updatedTitle);
                resource.GetProperty("goalAmount").GetDecimal().Should().Be(75000m);
                resource.GetProperty("collectedAmount").GetDecimal().Should().Be(31000m);
            }
        }
        finally
        {
            // Restore the seeded row whole: the table is single-row and shared by
            // all tests in the collection, so the seeded-values GET test must not
            // depend on execution order. Whole means the title too - it is a
            // required field of the request, so a restore without it is refused
            // with 400 and restores nothing at all.
            var restoreRequest = CreateAdminRequest(HttpMethod.Put, "/v1/fundraising");
            restoreRequest.Content = JsonContent.Create(new
            {
                title = SeededTitle,
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
        // Arrange - the title is valid, so the zero goal is the only thing the
        // refusal can be about
        var fundraisingData = new
        {
            title = SeededTitle,
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
        // Arrange - the title is valid, so the negative amount is the only thing
        // the refusal can be about
        var fundraisingData = new
        {
            title = SeededTitle,
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
