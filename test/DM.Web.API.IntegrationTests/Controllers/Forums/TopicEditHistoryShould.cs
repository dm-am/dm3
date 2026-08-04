using System.Net;
using System.Net.Http.Json;
using DM.Infrastructure.Persistence.Entities.Forum;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Forums;

/// <summary>
/// Who changed a topic, taken off the whole PATCH chain rather than off a mock.
/// </summary>
/// <remarks>
/// A topic row carries its author and nothing else, so the schema answers "who
/// changed this" with the TopicEdits history alone — declared since InitialCreate
/// and, until this, written by nobody. The consequence was not an empty table but
/// a hole in the blacklist: the ChangedTopic notification takes its actor from
/// this history, and with the history empty every topic edit was delivered to
/// subscribers who had blocked the person making it, while the mirror-image edit
/// of a blog publication was filtered.
///
/// Driven over HTTP because the field is set in the service and written in the
/// repository: a test that called either one alone would keep passing while the
/// two disagreed about who fills it.
/// </remarks>
public class TopicEditHistoryShould : IntegrationTestBase
{
    public TopicEditHistoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private async Task<Guid> AddTopicAsync(int topicNumber, string title)
    {
        var topicId = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        db.Set<Topic>().Add(new Topic
        {
            TopicId = topicId,
            BoardId = TestConstants.TestBoardId,
            AuthorId = TestConstants.TestUserId,
            Title = title,
            Text = "Topic body for the edit history check.",
            TopicNumber = topicNumber,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1),
            IsRemoved = false,
            IsAttached = false,
            IsClosed = false
        });
        await db.SaveChangesAsync();
        return topicId;
    }

    private async Task<List<TopicEdit>> EditsOfAsync(Guid topicId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        return await db.Set<TopicEdit>()
            .AsNoTracking()
            .Where(e => e.TopicId == topicId)
            .ToListAsync();
    }

    /// <summary>
    /// The editor is the person sending the request, not the person who opened
    /// the topic — the two are different here on purpose.
    /// </summary>
    [Fact]
    public async Task RecordTheEditorOfAChangedTopic()
    {
        var topicId = await AddTopicAsync(9101, "Topic before the edit");

        var request = CreateAdminRequest(HttpMethod.Patch, $"/v1/topics/{topicId}");
        request.Content = JsonContent.Create(new { title = "Topic after the edit" });
        var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);

        var edits = await EditsOfAsync(topicId);
        edits.Should().ContainSingle("one request that changed the topic is one edit");
        edits[0].EditorUserId.Should().Be(TestConstants.AdminUserId,
            "the history holds whoever sent the request; the topic's author is " +
            "the seeded test user and did not touch it");
        edits[0].EditorUserId.Should().NotBe(TestConstants.TestUserId);
        edits[0].EditedUtc.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    /// <summary>
    /// A request that round-trips the values the topic already holds leaves no
    /// edit behind.
    /// </summary>
    /// <remarks>
    /// The client PATCHes the fields it has, so the same values come back on
    /// every save the user cancels out of. Recording those would turn the
    /// history into a request log and hand the ChangedTopic notification an
    /// actor for a change nobody made.
    /// </remarks>
    [Fact]
    public async Task LeaveNoEditBehindWhenNothingChanges()
    {
        const string title = "Topic that keeps its title";
        var topicId = await AddTopicAsync(9102, title);

        var request = CreateAdminRequest(HttpMethod.Patch, $"/v1/topics/{topicId}");
        request.Content = JsonContent.Create(new { title });
        var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);

        var edits = await EditsOfAsync(topicId);
        edits.Should().BeEmpty("the request changed no column of the topic");
    }
}
