using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// Integration tests for TicketIntakeController
/// </summary>
public class TicketIntakeControllerShould : IntegrationTestBase
{
    public TicketIntakeControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task CreateTicket_AllowsAnonymousSubmission()
    {
        // Arrange
        var requestData = new
        {
            subtype = "Bug",
            subject = "Integration test subject",
            text = "Integration test text",
            contact = "tester@example.com"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/tickets", requestData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateTicket_ReturnsBadRequestForEmptyFields()
    {
        // Arrange
        var requestData = new
        {
            subtype = "UserComplaint",
            subject = "",
            text = ""
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/tickets", requestData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTicket_RejectsFilledHoneypot()
    {
        // Arrange - bots tend to fill every field, including the hidden one
        var requestData = new
        {
            subtype = "Bug",
            subject = "Bot subject",
            text = "Bot text",
            website = "https://spam.example.com"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/tickets", requestData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
