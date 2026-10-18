using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Infrastructure.Persistence.Entities.Forum;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Forums;

/// <summary>
/// A like on a topic is on the topic the reader is handed back.
/// </summary>
/// <remarks>
/// The topic projection carried a count of likes and no likers, and nothing
/// filled them after it: every read answered with an empty list. The page tells
/// the reader's own like by that list, so the heart of the one who had liked a
/// topic was empty after a reload, and the list of those who liked it was empty
/// however many had.
///
/// Written against HTTP, as the comment half of the same proof is
/// (<see cref="General.CommentLikesShould" />): the omission lived in a query,
/// which a test with a substituted repository steps straight over.
/// </remarks>
public class TopicLikesShould : IntegrationTestBase
{
    public TopicLikesShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static GeneralUser Author => CustomWebApplicationFactory.CreateTestUser();
    private static GeneralUser Liker => CustomWebApplicationFactory.CreateSecondUser();

    [Fact]
    public async Task CarryTheLikeOnTheSingleTopic()
    {
        var (topicId, _) = await AddTopicAsync(9401);
        await LikeAsync(topicId);

        var topic = await ReadAsync($"/v1/topics/{topicId}", Liker);

        Names(topic.GetProperty("resource")).Should().Equal([Liker.Username],
            "after a reload the liker has to see his own like to be able to take it back");
    }

    [Fact]
    public async Task CarryTheLikeOnTheListedTopic()
    {
        var (topicId, marker) = await AddTopicAsync(9402);
        await LikeAsync(topicId);

        var listing = await ReadAsync(
            $"/v1/boards/{TestConstants.TestBoardId}/topics?search={marker}&take=100", Author);

        var topic = listing.GetProperty("resources")
            .EnumerateArray()
            .Single(t => t.GetProperty("id").GetGuid() == topicId);
        Names(topic).Should().Equal([Liker.Username]);
    }

    /// <summary>
    /// The client replaces the topic it shows with the answer to an edit, so the
    /// likes have to be on that answer too.
    /// </summary>
    [Fact]
    public async Task CarryTheLikeOnTheAnswerToAnEdit()
    {
        var (topicId, marker) = await AddTopicAsync(9403);
        await LikeAsync(topicId);

        var request = CreateAuthenticatedRequest(HttpMethod.Patch, $"/v1/topics/{topicId}", Author);
        request.Content = JsonContent.Create(new { title = $"Edited {marker}" });
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Names(document.RootElement.GetProperty("resource")).Should().Equal([Liker.Username]);
    }

    /// <summary>
    /// The heart the reader filled empties again when he takes the like back: the
    /// read shows his like while it stands and stops showing it once it is gone.
    /// </summary>
    [Fact]
    public async Task ShowTheLikeUntilItIsTakenBack()
    {
        var (topicId, _) = await AddTopicAsync(9404);
        await LikeAsync(topicId);
        Names((await ReadAsync($"/v1/topics/{topicId}", Liker)).GetProperty("resource"))
            .Should().Equal([Liker.Username]);

        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Delete, $"/v1/topics/{topicId}/likes", Liker));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Names((await ReadAsync($"/v1/topics/{topicId}", Liker)).GetProperty("resource")).Should().BeEmpty();
    }

    /// <summary>
    /// A topic by the test user on the test board. The number is this class's own:
    /// the board is shared, and a topic number is unique on it.
    /// </summary>
    private async Task<(Guid TopicId, string Marker)> AddTopicAsync(int topicNumber)
    {
        var topicId = Guid.NewGuid();
        var marker = Guid.NewGuid().ToString("N");
        await using var db = DatabaseFixture.CreateDbContext();
        db.Set<Topic>().Add(new Topic
        {
            TopicId = topicId,
            BoardId = TestConstants.TestBoardId,
            AuthorId = Author.UserId,
            Title = $"Topic likes {marker}",
            Text = "Body",
            TopicNumber = topicNumber,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1),
            IsRemoved = false,
            IsAttached = false,
            IsClosed = false
        });
        await db.SaveChangesAsync();
        return (topicId, marker);
    }

    private async Task LikeAsync(Guid topicId)
    {
        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Post, $"/v1/topics/{topicId}/likes", Liker));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private async Task<JsonElement> ReadAsync(string address, GeneralUser reader)
    {
        var response = await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, address, reader));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static string[] Names(JsonElement topic) => topic
        .GetProperty("likes")
        .EnumerateArray()
        .Select(l => l.GetProperty("username").GetString()!)
        .ToArray();
}
