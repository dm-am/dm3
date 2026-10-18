using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Forum;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Forums;

/// <summary>
/// Who closes and reopens a topic, through the edit form's PATCH.
/// </summary>
/// <remarks>
/// The author closes their own topic and reopens it if they closed it; the board's
/// moderators and the administrators close and reopen any. A closed topic stays
/// shut to its author's edits, and the reopening request is the one that opens it,
/// text and all. Driven over HTTP
/// because the rule is split between the resolver and the service, and a test of
/// either half alone would keep passing while the two disagreed.
/// </remarks>
public class TopicClosingShould : IntegrationTestBase
{
    public TopicClosingShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task LetTheAuthorCloseAndReopenTheirTopic()
    {
        var topicId = await AddTopicAsync(9301, isClosed: false);
        var author = CustomWebApplicationFactory.CreateTestUser();

        var closed = await PatchAsync(topicId, author, new { isClosed = true });
        closed.Status.Should().Be(HttpStatusCode.OK, closed.Body);
        IsClosed(closed.Body).Should().BeTrue();
        ClosedByAuthor(closed.Body).Should().BeTrue();

        // The edit form sends the text with the box: reopening opens the topic to the
        // author's edit in the same request.
        var reopened = await PatchAsync(topicId, author,
            new { title = "Reopened by the author", description = "Body", isClosed = false });
        reopened.Status.Should().Be(HttpStatusCode.OK, reopened.Body);
        IsClosed(reopened.Body).Should().BeFalse();
        ClosedByAuthor(reopened.Body).Should().BeFalse();
        Title(reopened.Body).Should().Be("Reopened by the author");
    }

    /// <summary>
    /// What a moderator closed stays closed to the author, or the closing would last
    /// one click: it is usually closed to stop the discussion in it. Once the
    /// moderator reopens it, the topic is the author's to close again.
    /// </summary>
    [Fact]
    public async Task KeepATopicAModeratorClosedShutToItsAuthor()
    {
        var topicId = await AddTopicAsync(9307, isClosed: false);
        var author = CustomWebApplicationFactory.CreateTestUser();
        var moderator = CustomWebApplicationFactory.CreateModeratorUser();

        var byModerator = await PatchAsync(topicId, moderator, new { isClosed = true });
        byModerator.Status.Should().Be(HttpStatusCode.OK, byModerator.Body);
        ClosedByAuthor(byModerator.Body).Should().BeFalse();

        var byAuthor = await PatchAsync(topicId, author,
            new { title = "Reopened over the moderator", description = "Body", isClosed = false });
        byAuthor.Status.Should().Be(HttpStatusCode.Forbidden, byAuthor.Body);

        (await PatchAsync(topicId, moderator, new { isClosed = false })).Status.Should().Be(HttpStatusCode.OK);
        (await PatchAsync(topicId, author, new { isClosed = true })).Status.Should().Be(HttpStatusCode.OK);
        var reopened = await PatchAsync(topicId, author, new { isClosed = false });
        reopened.Status.Should().Be(HttpStatusCode.OK, reopened.Body);
        IsClosed(reopened.Body).Should().BeFalse();
    }

    [Fact]
    public async Task KeepATopicAModeratorClosedShutToItsAuthorsEdits()
    {
        var topicId = await AddTopicAsync(9302, isClosed: true, closedByAuthor: false);

        var edit = await PatchAsync(topicId, CustomWebApplicationFactory.CreateTestUser(),
            new { title = "Edited while closed", isClosed = true });

        edit.Status.Should().Be(HttpStatusCode.Forbidden, edit.Body);
    }

    [Fact]
    public async Task LetTheAuthorEditATopicTheyClosedThemselvesKeepingItClosed()
    {
        var topicId = await AddTopicAsync(9308, isClosed: true, closedByAuthor: true);

        var edit = await PatchAsync(topicId, CustomWebApplicationFactory.CreateTestUser(),
            new { title = "Edited while closed by the author", description = "Body", isClosed = true });

        edit.Status.Should().Be(HttpStatusCode.OK, edit.Body);
        IsClosed(edit.Body).Should().BeTrue();
        ClosedByAuthor(edit.Body).Should().BeTrue();
    }

    [Fact]
    public async Task RefuseClosingToAnyoneElseWhoDoesNotModerateTheBoard()
    {
        var topicId = await AddTopicAsync(9303, isClosed: false);

        var stranger = await PatchAsync(topicId, CustomWebApplicationFactory.CreateSecondUser(),
            new { isClosed = true });
        stranger.Status.Should().Be(HttpStatusCode.Forbidden, stranger.Body);

        // A moderator of the site who does not moderate this board never closed
        // topics, and still does not.
        var siteModerator = await PatchAsync(topicId, CustomWebApplicationFactory.CreateSeniorModeratorUser(),
            new { isClosed = true });
        siteModerator.Status.Should().Be(HttpStatusCode.Forbidden, siteModerator.Body);
    }

    /// <summary>
    /// Reopening is where the ban has to hold on its own: the request is not asked
    /// the edit rule of the closed topic, so the closing rule is the only gate, and
    /// a reopened topic would take the banned author's text with it.
    /// </summary>
    [Theory]
    [InlineData(9304, false, true)]
    [InlineData(9306, true, false)]
    public async Task TakeClosingAndReopeningFromABannedAuthor(int topicNumber, bool isClosed, bool closes)
    {
        // Closed by the author himself, so that the ban is the one thing refusing.
        var topicId = await AddTopicAsync(topicNumber, isClosed, closedByAuthor: isClosed);

        var request = CreateAuthenticatedRequest(HttpMethod.Patch, $"/v1/topics/{topicId}",
            CustomWebApplicationFactory.CreateTestUser());
        request.Headers.Add(TestAuthenticationMiddleware.TestUserAccessPolicyHeader,
            AccessPolicy.DemocraticBan.ToString());
        request.Content = JsonContent.Create(new { title = "Written under a ban", isClosed = closes });
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LetTheBoardModeratorAndTheAdministratorCloseAnyTopic()
    {
        var topicId = await AddTopicAsync(9305, isClosed: false);

        var byModerator = await PatchAsync(topicId, CustomWebApplicationFactory.CreateModeratorUser(),
            new { isClosed = true });
        byModerator.Status.Should().Be(HttpStatusCode.OK, byModerator.Body);
        IsClosed(byModerator.Body).Should().BeTrue();

        var byAdmin = await PatchAsync(topicId, CustomWebApplicationFactory.CreateAdminUser(),
            new { isClosed = false });
        byAdmin.Status.Should().Be(HttpStatusCode.OK, byAdmin.Body);
        IsClosed(byAdmin.Body).Should().BeFalse();
    }

    private async Task<Guid> AddTopicAsync(int topicNumber, bool isClosed, bool closedByAuthor = false)
    {
        var topicId = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        db.Set<Topic>().Add(new Topic
        {
            TopicId = topicId,
            BoardId = TestConstants.TestBoardId,
            AuthorId = TestConstants.TestUserId,
            Title = "Topic for the closing check",
            Text = "Body",
            TopicNumber = topicNumber,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1),
            IsRemoved = false,
            IsAttached = false,
            IsClosed = isClosed,
            ClosedByAuthor = closedByAuthor
        });
        await db.SaveChangesAsync();
        return topicId;
    }

    private async Task<(HttpStatusCode Status, string Body)> PatchAsync(Guid topicId, GeneralUser user, object body)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Patch, $"/v1/topics/{topicId}", user);
        request.Content = JsonContent.Create(body);
        var response = await Client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static bool IsClosed(string envelope)
    {
        using var document = JsonDocument.Parse(envelope);
        return document.RootElement.GetProperty("resource").TryGetProperty("isClosed", out var closed) &&
               closed.GetBoolean();
    }

    private static bool ClosedByAuthor(string envelope)
    {
        using var document = JsonDocument.Parse(envelope);
        return document.RootElement.GetProperty("resource").GetProperty("closedByAuthor").GetBoolean();
    }

    private static string? Title(string envelope)
    {
        using var document = JsonDocument.Parse(envelope);
        return document.RootElement.GetProperty("resource").GetProperty("title").GetString();
    }
}
