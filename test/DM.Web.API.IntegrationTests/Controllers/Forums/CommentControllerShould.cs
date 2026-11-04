using System.Net;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Forums;

public class CommentControllerShould : IntegrationTestBase
{
    public CommentControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetForumCommentsWithValidTopicReturnsOk()
    {
        var response = await Client.GetAsync($"/v1/topics/{TestConstants.TestTopicId}/comments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("resources");
    }

    /// <summary>
    /// Get comments of a topic id that never existed should return NotFound.
    /// Gone (410) is reserved for a topic row that exists but is soft-deleted.
    /// </summary>
    [Fact]
    public async Task GetForumCommentsWithNonExistentTopicReturnsNotFound()
    {
        var nonExistentTopicId = Guid.NewGuid();

        var response = await Client.GetAsync($"/v1/topics/{nonExistentTopicId}/comments");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Get non-existent comment should return NotFound, the code its delete answers.
    /// </summary>
    [Fact]
    public async Task GetForumCommentWithNonExistentIdReturnsNotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await Client.GetAsync($"/v1/forum/comments/{nonExistentId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetForumCommentWithInvalidGuidReturnsBadRequest()
    {
        var response = await Client.GetAsync("/v1/forum/comments/not-a-guid");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

}
