using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Community;

public class PollControllerShould : IntegrationTestBase
{
    public PollControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetPollsReturnsOk()
    {
        var response = await Client.GetAsync("/v1/polls");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostPollWithAuthRequiresSeniorModeratorRole()
    {
        var pollData = new
        {
            title = "Test Poll",
            options = new[] { "Option 1", "Option 2" }
        };
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/v1/polls");
        request.Content = JsonContent.Create(pollData);

        var response = await Client.SendAsync(request);

        // Regular user should be forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateAPollWhoseStartArrivesBehindTheClock()
    {
        // The body the form sends: the start it offers is now, which arrives behind this clock.
        var sent = DateTimeOffset.UtcNow;
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/v1/polls",
            CustomWebApplicationFactory.CreateSeniorModeratorUser());
        request.Content = JsonContent.Create(new
        {
            title = "Кто ведет игру?",
            details = (string?)null,
            startsUtc = sent.AddSeconds(-30),
            endsUtc = sent.AddDays(7),
            isAnonymous = true,
            options = new[] { "Первый", "Второй" }
        });

        var response = await Client.SendAsync(request);
        var answered = DateTimeOffset.UtcNow;
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Created, "body was: {0}", body);
        using var created = JsonDocument.Parse(body);
        var pollId = created.RootElement.GetProperty("resource").GetProperty("id").GetGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        var stored = await db.Polls.SingleAsync(p => p.PollId == pollId);
        stored.StartsUtc.Should().BeOnOrAfter(sent, "a start behind the clock is recorded as the moment it arrived")
            .And.BeOnOrBefore(answered);
    }
}
