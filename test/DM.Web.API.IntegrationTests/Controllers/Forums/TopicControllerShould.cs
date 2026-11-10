using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Infrastructure.Persistence.Entities.Forum;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Forums;

public class TopicControllerShould : IntegrationTestBase
{
    public TopicControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetBoardTopicsWithValidBoardReturnsOk()
    {
        var response = await Client.GetAsync($"/v1/boards/{Uri.EscapeDataString(TestConstants.TestBoardTitle)}/topics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("resources");
    }

    [Fact]
    public async Task GetBoardTopicsWithNonExistentBoardReturnsNotFound()
    {
        var nonExistentBoardId = Guid.NewGuid();

        var response = await Client.GetAsync($"/v1/boards/{nonExistentBoardId}/topics");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetBoardTopicsWithInvalidBoardIdReturnsError()
    {
        var response = await Client.GetAsync("/v1/boards/not-a-guid/topics");

        // An alias nothing answers to is a board that never existed
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTopicWithValidIdReturnsOk()
    {
        var response = await Client.GetAsync($"/v1/topics/{TestConstants.TestTopicId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("resource");
    }

    // Get a topic id that never existed should return NotFound; Gone is for a soft-deleted row.
    [Fact]
    public async Task GetTopicWithNonExistentIdReturnsNotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await Client.GetAsync($"/v1/topics/{nonExistentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Get a soft-deleted topic should return Gone: the service tells the two apart with an
    // existence probe.
    [Fact]
    public async Task GetTopicWithRemovedTopicReturnsGone()
    {
        var removedTopicId = Guid.NewGuid();
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            db.Set<Topic>().Add(new Topic
            {
                TopicId = removedTopicId,
                BoardId = TestConstants.TestBoardId,
                AuthorId = TestConstants.TestUserId,
                Title = "Removed Topic",
                Text = "Removed topic content.",
                TopicNumber = 9001,
                CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1),
                IsRemoved = true,
                IsAttached = false,
                IsClosed = false
            });
            await db.SaveChangesAsync();
        }

        var response = await Client.GetAsync($"/v1/topics/{removedTopicId}");

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task GetTopicWithInvalidGuidReturnsBadRequest()
    {
        var response = await Client.GetAsync("/v1/topics/not-a-guid");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Pinning a topic sends only that flag, as the client does, and the rest of the topic survives.
    /// </summary>
    [Fact]
    public async Task PatchTopicWithOnlyOneFlagKeepsEverythingElse()
    {
        var request = CreateAdminRequest(HttpMethod.Patch, $"/v1/topics/{TestConstants.TestTopicId}");
        request.Content = JsonContent.Create(new { isAttached = true });
        var response = await Client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", content);
        var topic = JsonDocument.Parse(content).RootElement.GetProperty("resource");
        topic.GetProperty("isAttached").GetBoolean().Should().BeTrue();
        topic.GetProperty("title").GetString().Should().NotBeNullOrEmpty();
    }

}
