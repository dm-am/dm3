using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

/// <summary>
/// Integration tests for TicketController
/// </summary>
public class TicketControllerShould : IntegrationTestBase
{
    public TicketControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetTickets_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/moderation/tickets");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetTickets_WithAuth_RequiresModeratorRole()
    {
        // Arrange
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/moderation/tickets");

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Regular user should be forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTicketStats_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/moderation/tickets/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyFiledTickets_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/moderation/tickets/mine");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyFiledTickets_TakesThePageAndReportsTheTotal()
    {
        // Both lists used to answer with everything and say nothing about it.
        // The parameters have to bind on the wire, not only in the signature,
        // and the envelope has to carry the total: a truncated answer that does
        // not say how much it truncated leaves the caller unable to ask for the
        // rest.
        var request = CreateAuthenticatedRequest(
            HttpMethod.Get, "/v1/moderation/tickets/mine?skip=0&take=1");

        var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);
        var paging = JsonDocument.Parse(body).RootElement.GetProperty("paging");
        paging.GetProperty("take").GetInt32().Should().Be(1);
        paging.TryGetProperty("total", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateTicket_RequiresAuthentication()
    {
        // Arrange
        var ticketData = new
        {
            targetUserLogin = TestConstants.SecondUserLogin,
            reason = "Spam",
            description = "Test report"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/moderation/tickets", ticketData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateTicketComment_RequiresAuthentication()
    {
        var response = await Client.PostAsJsonAsync(
            $"/v1/moderation/tickets/{Guid.NewGuid()}/comments", new { text = "Есть кто?" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A moderator's answer is the one the author waits for, and the author's
    /// reply is the turn of moderation again: the state says whose move it is, and
    /// each answer comes back as the ticket it left.
    /// </summary>
    [Fact]
    public async Task CreateTicketComment_PassesTheTurnBetweenModerationAndTheAuthor()
    {
        var ticketId = await FileATicket(CustomWebApplicationFactory.CreateTestUser());

        var question = await Answer(ticketId, "[b]Пришлите ссылку[/b]", CustomWebApplicationFactory.CreateAdminUser());
        var asked = await Ticket(question);
        asked.GetProperty("status").GetString().Should().Be("WaitingForUser",
            "an answer that chose no status waits for the author");
        var written = asked.GetProperty("comments").EnumerateArray().Single();
        written.GetProperty("author").GetProperty("username").GetString().Should().Be(TestConstants.AdminUserUsername);
        written.GetProperty("isGuest").GetBoolean().Should().BeFalse();
        written.GetProperty("text").GetString().Should().Contain("<strong>Пришлите ссылку</strong>");

        var waitingForUser = await Read(ticketId, CustomWebApplicationFactory.CreateTestUser());
        waitingForUser.GetProperty("status").GetString().Should().Be("WaitingForUser",
            "the moderator asked, and the author is the one to answer");
        waitingForUser.GetProperty("commentsCount").GetInt32().Should().Be(1);
        waitingForUser.GetProperty("canComment").GetBoolean().Should().BeTrue();
        waitingForUser.GetProperty("canResolve").GetBoolean().Should().BeFalse(
            "the author does not choose the status of his own ticket");

        var reply = await Ticket(await Answer(ticketId, "Вот она", CustomWebApplicationFactory.CreateTestUser()));
        reply.GetProperty("status").GetString().Should().Be("WaitingForModeration");

        var waitingForModeration = await Read(ticketId, CustomWebApplicationFactory.CreateAdminUser());
        waitingForModeration.GetProperty("status").GetString().Should().Be("WaitingForModeration",
            "the author answered, and moderation is the one to read it");
        waitingForModeration.GetProperty("canResolve").GetBoolean().Should().BeTrue();
        var comments = waitingForModeration.GetProperty("comments").EnumerateArray().ToArray();
        comments.Select(c => c.GetProperty("author").GetProperty("username").GetString())
            .Should().Equal([TestConstants.AdminUserUsername, TestConstants.TestUserUsername],
                "the conversation reads from the oldest comment to the newest");
    }

    /// <summary>
    /// Whoever cannot read a ticket cannot write in it either, and cannot tell it
    /// from a ticket that does not exist.
    /// </summary>
    [Fact]
    public async Task CreateTicketComment_AnswersAStrangerAsIfThereWereNoTicket()
    {
        var ticketId = await FileATicket(CustomWebApplicationFactory.CreateTestUser());

        var response = await Answer(ticketId, "Я тут мимо", CustomWebApplicationFactory.CreateSecondUser());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Read(ticketId, CustomWebApplicationFactory.CreateTestUser()))
            .GetProperty("commentsCount").GetInt32().Should().Be(0);
    }

    /// <summary>
    /// A closed ticket takes no answer and is not opened again by one.
    /// </summary>
    [Fact]
    public async Task CreateTicketComment_RefusesAClosedTicket()
    {
        var ticketId = await FileATicket(CustomWebApplicationFactory.CreateTestUser());
        (await Answer(ticketId, "", CustomWebApplicationFactory.CreateAdminUser(), "Closed")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        var response = await Answer(ticketId, "А еще вот что", CustomWebApplicationFactory.CreateTestUser());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var ticket = await Read(ticketId, CustomWebApplicationFactory.CreateTestUser());
        ticket.GetProperty("status").GetString().Should().Be("Closed");
        ticket.GetProperty("commentsCount").GetInt32().Should().Be(0);
        ticket.GetProperty("canComment").GetBoolean().Should().BeFalse();
    }

    /// <summary>
    /// An answer of a ticket is held to the length of the ticket itself, not to
    /// the length of a comment of a topic.
    /// </summary>
    [Fact]
    public async Task CreateTicketComment_HoldsTheTextToTheLengthOfATicket()
    {
        var ticketId = await FileATicket(CustomWebApplicationFactory.CreateTestUser());

        var tooLong = await Answer(ticketId, new string('а', 10001), CustomWebApplicationFactory.CreateTestUser());
        var longest = await Answer(ticketId, new string('а', 10000), CustomWebApplicationFactory.CreateTestUser());

        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        longest.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The last word and the closing are one answer: the comment lands, the ticket
    /// is closed in the name of the moderator who wrote it, and nothing more is
    /// offered on it.
    /// </summary>
    [Fact]
    public async Task CreateTicketComment_ClosesWithTheLastWordInOneAnswer()
    {
        var ticketId = await FileATicket(CustomWebApplicationFactory.CreateTestUser());

        var closed = await Ticket(await Answer(ticketId, "Исправили, спасибо",
            CustomWebApplicationFactory.CreateAdminUser(), "Closed"));

        closed.GetProperty("status").GetString().Should().Be("Closed");
        closed.GetProperty("resolvedByUsername").GetString().Should().Be(TestConstants.AdminUserUsername);
        closed.GetProperty("canComment").GetBoolean().Should().BeFalse();
        closed.GetProperty("canResolve").GetBoolean().Should().BeFalse();
        closed.GetProperty("comments").EnumerateArray().Single()
            .GetProperty("author").GetProperty("username").GetString().Should().Be(TestConstants.AdminUserUsername);

        var author = await Read(ticketId, CustomWebApplicationFactory.CreateTestUser());
        author.GetProperty("status").GetString().Should().Be("Closed");
        author.GetProperty("commentsCount").GetInt32().Should().Be(1);
    }

    /// <summary>
    /// A closing needs no word: an empty answer with a status that ends the ticket
    /// only moves the status, the ticket names who closed it, and it is closed
    /// once.
    /// </summary>
    [Theory]
    [InlineData("Closed")]
    [InlineData("Spam")]
    public async Task CreateTicketComment_ClosesWithoutAWordAndNamesWhoClosedIt(string status)
    {
        var ticketId = await FileATicket(CustomWebApplicationFactory.CreateTestUser());

        var resolved = await Answer(ticketId, "", CustomWebApplicationFactory.CreateAdminUser(), status);

        resolved.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", await resolved.Content.ReadAsStringAsync());
        var ticket = await Read(ticketId, CustomWebApplicationFactory.CreateTestUser());
        ticket.GetProperty("status").GetString().Should().Be(status);
        ticket.GetProperty("resolvedByUsername").GetString().Should().Be(TestConstants.AdminUserUsername);
        ticket.GetProperty("commentsCount").GetInt32().Should().Be(0, "nothing was said");

        var again = await Answer(ticketId, "", CustomWebApplicationFactory.CreateAdminUser(), "Closed");
        again.StatusCode.Should().Be(HttpStatusCode.BadRequest, "a ticket is closed once");
    }

    /// <summary>
    /// An answer that passes the turn to the author has to say something to him,
    /// and waiting for moderation is the author's turn, not a choice of moderation.
    /// </summary>
    [Theory]
    [InlineData("", null, "text")]
    [InlineData("", "WaitingForUser", "text")]
    [InlineData("Ответ", "WaitingForModeration", "status")]
    public async Task CreateTicketComment_RefusesAnAnswerItCannotTake(string text, string? status, string field)
    {
        var ticketId = await FileATicket(CustomWebApplicationFactory.CreateTestUser());

        var response = await Answer(ticketId, text, CustomWebApplicationFactory.CreateAdminUser(), status);

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "body was: {0}", body);
        JsonDocument.Parse(body).RootElement.GetProperty("errors").EnumerateObject()
            .Select(error => error.Name.ToLowerInvariant()).Should().Contain(field);
        (await Read(ticketId, CustomWebApplicationFactory.CreateTestUser()))
            .GetProperty("status").GetString().Should().Be("WaitingForModeration", "nothing was written");
    }

    /// <summary>
    /// The side of a ticket does not judge it: the moderator who filed it chooses
    /// no status, and answers in it as its author.
    /// </summary>
    [Fact]
    public async Task CreateTicketComment_RefusesAStatusFromTheModeratorWhoFiledIt()
    {
        var moderator = CustomWebApplicationFactory.CreateModeratorUser();
        var ticketId = await FileAComplaint(moderator, TestConstants.SecondUserUsername);

        var own = await Read(ticketId, moderator);
        own.GetProperty("canResolve").GetBoolean().Should().BeFalse();

        var resolved = await Answer(ticketId, "Закрываю", moderator, "Spam");
        resolved.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Read(ticketId, moderator)).GetProperty("commentsCount").GetInt32().Should().Be(0,
            "an answer that asked for a status it may not choose is refused whole");

        (await Answer(ticketId, "Дополню", moderator)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Read(ticketId, moderator)).GetProperty("status").GetString().Should().Be("WaitingForModeration",
            "his answer is a reply of the author, whatever his role");
    }

    /// <summary>
    /// A complaint about a moderator does not reach the moderator it names: not
    /// to read, not to write in, not to close, not in the queue.
    /// </summary>
    [Fact]
    public async Task HideAComplaintFromTheModeratorItNames()
    {
        var named = CustomWebApplicationFactory.CreateModeratorUser();
        var ticketId = await FileAComplaint(CustomWebApplicationFactory.CreateTestUser(), named.Username);

        (await Client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Get, $"/v1/moderation/tickets/{ticketId}", named)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Answer(ticketId, "Это неправда", named)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Answer(ticketId, "", named, "Spam")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Queue(named, "UserComplaint")).Should().NotContain(ticketId);
        (await Queue(named)).Should().NotContain(ticketId);

        var senior = CustomWebApplicationFactory.CreateSeniorModeratorUser();
        (await Read(ticketId, senior)).GetProperty("status").GetString().Should().Be("WaitingForModeration",
            "the rest of moderation still sees it, and nobody closed it on the way");
        (await Queue(senior, "UserComplaint")).Should().Contain(ticketId);
    }

    /// <summary>
    /// The complaint is read as its text: the violator named on the form is a line
    /// of it, spelled as the site spells the name, and the ticket carries no field
    /// beside the text for him, nor anything of a sanction.
    /// </summary>
    [Fact]
    public async Task WriteTheViolatorIntoTheTextOfTheComplaint()
    {
        var ticketId = await FileAComplaint(CustomWebApplicationFactory.CreateTestUser(), "SecondUser");

        var ticket = await Read(ticketId, CustomWebApplicationFactory.CreateAdminUser());

        ticket.GetProperty("description").GetString().Should().Be(
            "Оскорбляет в комментариях\n\n" +
            $"Нарушитель: {TestConstants.SecondUserUsername}\n\n" +
            "Ссылка на нарушение: https://dm.am/forum/general/1");
        ticket.GetProperty("descriptionHtml").GetString().Should().Contain(TestConstants.SecondUserUsername,
            "the line is rendered with the rest of the text");
        foreach (var gone in new[] { "targetUsername", "hasWarning", "hasBan", "canIssueWarning", "canIssueBan" })
        {
            ticket.TryGetProperty(gone, out _).Should().BeFalse($"{gone} is not a part of a ticket any more");
        }
    }

    /// <summary>
    /// One queue for moderation, and it is no wider than the rule that opens a
    /// ticket (TicketVisibility): a junior moderator reads complaints about users,
    /// a senior one the complaints about a junior's decision on top of that, and
    /// the administrators everything, the support desk included.
    /// </summary>
    [Fact]
    public async Task HoldTheOneQueueToWhatTheRoleReads()
    {
        var author = CustomWebApplicationFactory.CreateTestUser();
        var complaint = await FileAComplaint(author, TestConstants.SecondUserUsername);
        var juniorDecision = await FileADecisionComplaint(author, "ModeratorDecisionComplaint");
        var seniorDecision = await FileADecisionComplaint(author, "SeniorModeratorDecisionComplaint");
        var bug = await FileATicket(author);
        var recovery = await FileAsAGuest("AccessRecovery");

        var junior = await Queue(CustomWebApplicationFactory.CreateModeratorUser());
        var senior = await Queue(CustomWebApplicationFactory.CreateSeniorModeratorUser());
        var admin = await Queue(CustomWebApplicationFactory.CreateAdminUser());

        junior.Should().Contain(complaint)
            .And.NotContain([juniorDecision, seniorDecision, bug, recovery]);
        senior.Should().Contain([complaint, juniorDecision])
            .And.NotContain([seniorDecision, bug, recovery]);
        admin.Should().Contain([complaint, juniorDecision, seniorDecision, bug, recovery]);
    }

    /// <summary>
    /// The queue stands in the order of whose turn it is: the tickets waiting for
    /// moderation before the answered ones and the closed ones, newest first
    /// within each, across the whole queue and not page by page.
    /// </summary>
    [Fact]
    public async Task StandTheQueueInTheOrderOfStatusesThenNewestFirst()
    {
        var author = CustomWebApplicationFactory.CreateTestUser();
        var admin = CustomWebApplicationFactory.CreateAdminUser();
        var waiting = await FileATicket(author);
        var closed = await FileATicket(author);
        (await Answer(closed, "", admin, "Closed")).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, "/v1/moderation/tickets?skip=0&take=100", admin));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);
        var page = JsonDocument.Parse(body).RootElement.GetProperty("resources").EnumerateArray()
            .Select(t => (
                Id: t.GetProperty("id").GetGuid(),
                Rank: Array.IndexOf(StatusOrder, t.GetProperty("status").GetString()),
                Created: t.GetProperty("createdUtc").GetDateTimeOffset()))
            .ToArray();

        page.Select(t => t.Id).Should().Contain(waiting);
        page.Select(t => t.Rank).Should().BeInAscendingOrder();
        foreach (var group in page.GroupBy(t => t.Rank))
        {
            group.Select(t => t.Created).Should().BeInDescendingOrder();
        }

        var closedAt = Array.FindIndex(page, t => t.Id == closed);
        if (closedAt >= 0)
        {
            Array.FindIndex(page, t => t.Id == waiting).Should().BeLessThan(closedAt,
                "the older ticket still waits for moderation, the newer one is over");
        }
    }

    /// <summary>
    /// Nothing is taken in hand any more, and nothing is closed apart from an
    /// answer: the addresses of the assignment and of the separate closing answer
    /// nothing.
    /// </summary>
    [Fact]
    public async Task AnswerNothingAtTheAddressesOfTheAssignmentAndTheClosing()
    {
        var ticketId = await FileATicket(CustomWebApplicationFactory.CreateTestUser());

        var assign = await Client.SendAsync(
            CreateAdminRequest(HttpMethod.Post, $"/v1/moderation/tickets/{ticketId}/assign"));
        var assigned = await Client.SendAsync(
            CreateAdminRequest(HttpMethod.Get, "/v1/moderation/tickets/assigned"));
        var resolve = CreateAdminRequest(HttpMethod.Post, $"/v1/moderation/tickets/{ticketId}/resolve");
        resolve.Content = JsonContent.Create(new { status = "Closed" });
        var resolved = await Client.SendAsync(resolve);

        assign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        assigned.StatusCode.Should().Be(HttpStatusCode.NotFound);
        resolved.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Read(ticketId, CustomWebApplicationFactory.CreateTestUser()))
            .GetProperty("status").GetString().Should().Be("WaitingForModeration");
    }

    /// <summary>The statuses in the order the queue stands in.</summary>
    private static readonly string[] StatusOrder = ["WaitingForModeration", "WaitingForUser", "Closed", "Spam"];

    /// <summary>A ticket filed through the form, by its identifier.</summary>
    private async Task<Guid> FileATicket(GeneralUser author)
    {
        var post = CreateAuthenticatedRequest(HttpMethod.Post, "/v1/tickets", author);
        post.Content = JsonContent.Create(new { subtype = "Bug", text = "Не открывается страница" });
        return await Filed(post);
    }

    /// <summary>A complaint about the named user, filed through the form.</summary>
    private async Task<Guid> FileAComplaint(GeneralUser author, string violator)
    {
        var post = CreateAuthenticatedRequest(HttpMethod.Post, "/v1/tickets", author);
        post.Content = JsonContent.Create(new
        {
            subtype = "UserComplaint",
            text = "Оскорбляет в комментариях",
            targetUsername = violator,
            violationUrl = "https://dm.am/forum/general/1"
        });
        return await Filed(post);
    }

    /// <summary>A complaint about a decision of moderation, filed through the form.</summary>
    private async Task<Guid> FileADecisionComplaint(GeneralUser author, string subtype)
    {
        var post = CreateAuthenticatedRequest(HttpMethod.Post, "/v1/tickets", author);
        post.Content = JsonContent.Create(new
        {
            subtype,
            text = "Прошу пересмотреть решение",
            violationUrl = "https://dm.am/warnings?username=testuser"
        });
        return await Filed(post);
    }

    /// <summary>A ticket a guest filed through the form, with the address he left.</summary>
    private async Task<Guid> FileAsAGuest(string subtype)
    {
        var post = new HttpRequestMessage(HttpMethod.Post, "/v1/tickets")
        {
            Content = JsonContent.Create(new
            {
                subtype,
                text = "Не могу войти",
                contact = "guest@example.com"
            })
        };
        return await Filed(post);
    }

    private async Task<Guid> Filed(HttpRequestMessage post)
    {
        var created = await Client.SendAsync(post);
        var body = await created.Content.ReadAsStringAsync();
        created.StatusCode.Should().Be(HttpStatusCode.Created, "body was: {0}", body);
        return JsonDocument.Parse(body).RootElement.GetProperty("ticketId").GetGuid();
    }

    /// <summary>
    /// An answer in the conversation, with the status moderation chose with it
    /// when one is given.
    /// </summary>
    private Task<HttpResponseMessage> Answer(Guid ticketId, string text, GeneralUser user, string? status = null)
    {
        var post = CreateAuthenticatedRequest(HttpMethod.Post, $"/v1/moderation/tickets/{ticketId}/comments", user);
        post.Content = status == null
            ? JsonContent.Create(new { text })
            : JsonContent.Create(new { text, status });
        return Client.SendAsync(post);
    }

    /// <summary>The ticket an answer came back with.</summary>
    private static async Task<JsonElement> Ticket(HttpResponseMessage answer)
    {
        var body = await answer.Content.ReadAsStringAsync();
        answer.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);
        return JsonDocument.Parse(body).RootElement.GetProperty("resource").Clone();
    }

    /// <summary>The ticket page as the given user reads it.</summary>
    private async Task<JsonElement> Read(Guid ticketId, GeneralUser user)
    {
        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/moderation/tickets/{ticketId}", user));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);
        return JsonDocument.Parse(body).RootElement.GetProperty("resource").Clone();
    }

    /// <summary>
    /// The identifiers of the first page of the queue as the given moderator reads
    /// it, narrowed to one reason when one is given.
    /// </summary>
    private async Task<Guid[]> Queue(GeneralUser user, string? subtype = null)
    {
        var filter = subtype == null ? "" : $"subtype={subtype}&";
        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/moderation/tickets?{filter}skip=0&take=100", user));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);
        return JsonDocument.Parse(body).RootElement.GetProperty("resources").EnumerateArray()
            .Select(ticket => ticket.GetProperty("id").GetGuid())
            .ToArray();
    }
}
