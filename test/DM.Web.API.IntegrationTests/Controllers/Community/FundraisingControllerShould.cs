using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Community;

public class FundraisingControllerShould : IntegrationTestBase
{
    // The single seeded row, field for field (CommunityModel, Fundraising): empty,
    // nothing announced and nothing collected. The title belongs here as much as
    // the amounts do, since a restore puts the row back whole.
    private const string SeededTitle = "";
    private const decimal SeededGoalAmount = 0m;
    private const decimal SeededCollectedAmount = 0m;

    // A collection an administrator could announce: what the refusals are measured against.
    private const string AnnouncedTitle = "Хостинг на год";

    public FundraisingControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetFundraisingReturnsOkWithSeededValues()
    {
        // Available anonymously
        var response = await Client.GetAsync("/v1/fundraising");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var resource = doc.RootElement.GetProperty("resource");
        resource.GetProperty("title").GetString().Should().Be(SeededTitle);
        resource.GetProperty("goalAmount").GetDecimal().Should().Be(SeededGoalAmount);
        resource.GetProperty("collectedAmount").GetDecimal().Should().Be(SeededCollectedAmount);
    }

    [Fact]
    public async Task PutFundraisingWithAuthRequiresAdminRole()
    {
        var fundraisingData = new
        {
            title = "Новый сервер",
            goalAmount = 60000,
            collectedAmount = 20000
        };
        var request = CreateAuthenticatedRequest(HttpMethod.Put, "/v1/fundraising");
        request.Content = JsonContent.Create(fundraisingData);

        var response = await Client.SendAsync(request);

        // Regular user should be forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutFundraisingAsAdminUpdatesAndPersistsValues()
    {
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
            var response = await Client.SendAsync(request);

            // Response carries the updated values
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = await response.Content.ReadAsStringAsync();
            using (var doc = JsonDocument.Parse(json))
            {
                var resource = doc.RootElement.GetProperty("resource");
                resource.GetProperty("title").GetString().Should().Be(updatedTitle);
                resource.GetProperty("goalAmount").GetDecimal().Should().Be(75000m);
                resource.GetProperty("collectedAmount").GetDecimal().Should().Be(31000m);
            }

            // Values are persisted (visible to a subsequent anonymous GET)
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
            // Restore the seeded row whole, title included: the table is single-row and
            // shared. Written to the table, since the form refuses an empty goal.
            await using var db = DatabaseFixture.CreateDbContext();
            await db.FundraisingGoals.ExecuteUpdateAsync(goal => goal
                .SetProperty(g => g.Title, SeededTitle)
                .SetProperty(g => g.GoalAmount, SeededGoalAmount)
                .SetProperty(g => g.CollectedAmount, SeededCollectedAmount));
        }
    }

    /// <remarks>The title is valid, so the amount is the only thing the refusal can be about.</remarks>
    [Theory]
    [InlineData(0, 1000)]
    [InlineData(50000, -1)]
    public async Task RefuseAnAmountOutOfRange(int goalAmount, int collectedAmount)
    {
        var request = CreateAdminRequest(HttpMethod.Put, "/v1/fundraising");
        request.Content = JsonContent.Create(new { title = AnnouncedTitle, goalAmount, collectedAmount });

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
