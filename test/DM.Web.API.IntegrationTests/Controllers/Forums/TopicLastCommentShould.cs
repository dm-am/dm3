using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Infrastructure.Persistence.Entities.Forum;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Forums;

/// <summary>
/// A listed topic names its last comment by the comment's own identifier.
/// </summary>
/// <remarks>
/// The topic projection filled the date and the author of the last comment and
/// left its identifier at the default, on the word that nobody read it. The
/// topic lists read it: the date in the "last comment" column links to
/// #comment-&lt;id&gt;, and with an empty identifier every such link opened the
/// topic at its top instead of at the comment.
///
/// Written against HTTP: the omission lived in a query, which a test with a
/// substituted repository steps straight over.
/// </remarks>
public class TopicLastCommentShould : IntegrationTestBase
{
    public TopicLastCommentShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static GeneralUser Author => CustomWebApplicationFactory.CreateTestUser();

    /// <summary>The board list and the list across boards (the profile's topics).</summary>
    public static TheoryData<string, int> Lists => new()
    {
        { $"/v1/boards/{TestConstants.TestBoardId}/topics", 9501 },
        { "/v1/topics", 9502 },
    };

    [Theory]
    [MemberData(nameof(Lists))]
    public async Task NameTheLastCommentOfTheListedTopic(string list, int topicNumber)
    {
        var (topicId, marker) = await AddTopicAsync(topicNumber);
        await CommentAsync(topicId, "First");
        var last = await CommentAsync(topicId, "Last");

        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, $"{list}?search={marker}&take=100", Author));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var topic = document.RootElement.GetProperty("resources")
            .EnumerateArray()
            .Single(t => t.GetProperty("id").GetGuid() == topicId);

        topic.GetProperty("lastComment").GetProperty("id").GetGuid().Should().Be(last,
            "the list links to the last comment by this identifier");
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
            Title = $"Topic last comment {marker}",
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

    /// <summary>
    /// Through the route the page uses, so the topic's last-comment pointer is
    /// moved by the write path itself and not by the test.
    /// </summary>
    private async Task<Guid> CommentAsync(Guid topicId, string text)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Post, $"/v1/topics/{topicId}/comments", Author);
        request.Content = JsonContent.Create(new { text });
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("resource").GetProperty("id").GetGuid();
    }
}
