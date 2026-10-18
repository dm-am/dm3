using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

    [Theory]
    [InlineData("AccountRestoration")]
    [InlineData("PersonalData")]
    [InlineData("AgreementQuestion")]
    public async Task CreateTicket_AcceptsAccountCategoriesFromAGuest(string subtype)
    {
        // Restoring an account is asked for by an ordinary support ticket, and
        // the person asking is signed out; so is a request about one's data or a
        // question about the agreement, which the legal pages send their reader
        // with. This also pins the wire spelling of the categories: the client
        // sends the enum name, so a rename on either side must fail here and not
        // in the browser.
        var requestData = new
        {
            subtype,
            subject = "Прошу удалить аккаунт",
            text = "Прошу обработать мой аккаунт",
            contact = "tester@example.com"
        };

        var response = await Client.PostAsJsonAsync("/v1/tickets", requestData);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// A complaint about a person and a request to remove an account are filed
    /// from an account: a guest is refused under the reason, and nothing is
    /// filed.
    /// </summary>
    [Theory]
    [InlineData("UserComplaint")]
    [InlineData("AccountRemoval")]
    public async Task CreateTicket_RefusesAGuestTheReasonsFiledFromAnAccount(string subtype)
    {
        var response = await Client.PostAsJsonAsync("/v1/tickets", new
        {
            subtype,
            text = "Прошу разобраться",
            contact = "tester@example.com",
            targetUsername = TestConstants.SecondUserLogin,
            violationUrl = "https://dm.am/forum/flood/1"
        });

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "body was: {0}", body);
        JsonDocument.Parse(body).RootElement.GetProperty("errors").EnumerateObject()
            .Select(field => field.Name.ToLowerInvariant()).Should().Contain("subtype");
    }

    /// <summary>
    /// The violator of a complaint is named and found; an unknown name is
    /// refused under its own field rather than failing the whole form.
    /// </summary>
    [Fact]
    public async Task CreateTicket_RefusesAnUnknownViolatorUnderItsField()
    {
        var post = CreateAuthenticatedRequest(HttpMethod.Post, "/v1/tickets");
        post.Content = JsonContent.Create(new
        {
            subtype = "UserComplaint",
            text = "Оскорбляет в комментариях",
            targetUsername = "nobodycarriesthisname",
            violationUrl = "https://dm.am/forum/flood/1"
        });

        var response = await Client.SendAsync(post);

        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "body was: {0}", body);
        JsonDocument.Parse(body).RootElement.GetProperty("errors").EnumerateObject()
            .Select(field => field.Name.ToLowerInvariant()).Should().Contain("targetusername");
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
    public async Task CreateTicket_AcceptsATicketWithoutSubject_AndTracksItRendered()
    {
        // The support form sends no subject and writes the text in the site
        // markup. The guest tracking page gets the text as written, the address
        // the guest left a line under it, and rendered next to it, with an empty
        // subject the page replaces by the reason.
        var marker = Guid.NewGuid().ToString("N");
        var text = $"[b]{marker}[/b] не открывается";

        var created = await Client.PostAsJsonAsync("/v1/tickets", new
        {
            subtype = "Bug",
            text,
            contact = "tester@example.com"
        });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var answer = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement;
        answer.GetProperty("ticketId").GetGuid().Should().NotBeEmpty(
            "the form goes on to the page of the ticket it has just filed");
        var token = answer.GetProperty("trackingToken").GetString();
        token.Should().NotBeNullOrEmpty();

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/tickets/track");
        request.Headers.Add("X-Dm-Ticket-Token", token);
        var tracked = await Client.SendAsync(request);

        tracked.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = JsonDocument.Parse(await tracked.Content.ReadAsStringAsync())
            .RootElement.GetProperty("resource");
        data.GetProperty("subject").GetString().Should().BeEmpty();
        data.GetProperty("description").GetString().Should()
            .Be(text + "\n\nКонтакт для ответа: tester@example.com");
        data.GetProperty("descriptionHtml").GetString().Should()
            .Contain($"<strong>{marker}</strong>")
            .And.NotContain("[b]");
    }

    [Fact]
    public async Task CreateTicket_AndShowItsAuthorTheTextRendered()
    {
        // "Мои обращения" reads the moderation ticket list, and it gets the same
        // rendered field as the tracking page and the moderation card.
        var marker = Guid.NewGuid().ToString("N");
        var post = CreateAuthenticatedRequest(HttpMethod.Post, "/v1/tickets");
        post.Content = JsonContent.Create(new { subtype = "Bug", text = $"[i]{marker}[/i]" });

        var created = await Client.SendAsync(post);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var mine = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, "/v1/moderation/tickets/mine?skip=0&take=100"));
        mine.StatusCode.Should().Be(HttpStatusCode.OK);

        var ticket = JsonDocument.Parse(await mine.Content.ReadAsStringAsync())
            .RootElement.GetProperty("resources").EnumerateArray()
            .Single(item => item.GetProperty("description").GetString()!.Contains(marker));
        ticket.GetProperty("descriptionHtml").GetString().Should().Contain($"<em>{marker}</em>");
    }

    [Fact]
    public async Task CreateTicket_GivesItsAuthorTheTicketToOpen()
    {
        // The form of a signed-in author goes on to "/my-tickets/<id>", so the
        // intake answers with the identifier, and the identifier opens the ticket
        // for its author. No token: an account is how he finds it again.
        var marker = Guid.NewGuid().ToString("N");
        var post = CreateAuthenticatedRequest(HttpMethod.Post, "/v1/tickets");
        post.Content = JsonContent.Create(new { subtype = "Bug", text = $"[i]{marker}[/i]" });

        var created = await Client.SendAsync(post);
        var body = await created.Content.ReadAsStringAsync();
        created.StatusCode.Should().Be(HttpStatusCode.Created, "body was: {0}", body);

        var answer = JsonDocument.Parse(body).RootElement;
        var ticketId = answer.GetProperty("ticketId").GetGuid();
        ticketId.Should().NotBeEmpty();
        (!answer.TryGetProperty("trackingToken", out var token) || token.ValueKind == JsonValueKind.Null)
            .Should().BeTrue("a signed-in author finds the ticket by his account, not by a token");

        var read = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/moderation/tickets/{ticketId}"));
        var readBody = await read.Content.ReadAsStringAsync();
        read.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", readBody);
        JsonDocument.Parse(readBody).RootElement.GetProperty("resource")
            .GetProperty("descriptionHtml").GetString().Should().Contain($"<em>{marker}</em>");
    }

    [Fact]
    public async Task CreateTicketComment_ShowsItsAuthorTheAnswerRendered()
    {
        // Moderation answers in the conversation, in the editor of comments. The
        // author reads the answer rendered, signed by the moderator who wrote it.
        var marker = Guid.NewGuid().ToString("N");
        var ticketId = await FileAsTheTestUser("Не открывается страница");

        var comment = CreateAdminRequest(HttpMethod.Post, $"/v1/moderation/tickets/{ticketId}/comments");
        comment.Content = JsonContent.Create(new { text = $"[b]{marker}[/b]" });
        var commented = await Client.SendAsync(comment);
        commented.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}",
            await commented.Content.ReadAsStringAsync());

        var read = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/moderation/tickets/{ticketId}"));
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        var ticket = JsonDocument.Parse(await read.Content.ReadAsStringAsync()).RootElement.GetProperty("resource");
        var answer = ticket.GetProperty("comments").EnumerateArray().Single();
        answer.GetProperty("text").GetString().Should()
            .Contain($"<strong>{marker}</strong>")
            .And.NotContain("[b]");
        answer.GetProperty("author").GetProperty("username").GetString().Should().Be(TestConstants.AdminUserUsername);
        ticket.TryGetProperty("answer", out _).Should().BeFalse("the single answer is gone with the conversation");
    }

    [Fact]
    public async Task CreateTrackedTicketComment_LetsTheGuestAnswerByTheToken()
    {
        // The guest writes on his tracking page with the token as the credential,
        // signed by nobody; moderation answers him in the same conversation.
        var marker = Guid.NewGuid().ToString("N");
        var (ticketId, token) = await FileAsAGuest("Не приходит письмо");

        var written = await CommentAsTheGuest(token, $"[i]{marker}[/i]");
        written.StatusCode.Should().Be(HttpStatusCode.Created, "body was: {0}",
            await written.Content.ReadAsStringAsync());
        var own = JsonDocument.Parse(await written.Content.ReadAsStringAsync()).RootElement.GetProperty("resource");
        own.GetProperty("isGuest").GetBoolean().Should().BeTrue();
        (!own.TryGetProperty("author", out var author) || author.ValueKind == JsonValueKind.Null)
            .Should().BeTrue("the comment of the guest is signed by nobody");

        var answer = CreateAdminRequest(HttpMethod.Post, $"/v1/moderation/tickets/{ticketId}/comments");
        answer.Content = JsonContent.Create(new { text = "Проверьте папку со спамом" });
        (await Client.SendAsync(answer)).StatusCode.Should().Be(HttpStatusCode.OK);

        var data = await Track(token);
        data.GetProperty("status").GetString().Should().Be("WaitingForUser");
        data.GetProperty("canComment").GetBoolean().Should().BeTrue();
        var comments = data.GetProperty("comments").EnumerateArray().ToArray();
        comments.Should().HaveCount(2);
        comments[0].GetProperty("isGuest").GetBoolean().Should().BeTrue();
        comments[0].GetProperty("text").GetString().Should().Contain($"<em>{marker}</em>");
        comments[1].GetProperty("author").GetProperty("username").GetString()
            .Should().Be(TestConstants.AdminUserUsername, "the guest sees who answered him, as the author does");
    }

    [Fact]
    public async Task CreateTrackedTicketComment_RefusesAClosedTicket()
    {
        var (ticketId, token) = await FileAsAGuest("Не приходит письмо");
        // Moderation closes it with an answer that says nothing: the status is the
        // whole answer.
        var close = CreateAdminRequest(HttpMethod.Post, $"/v1/moderation/tickets/{ticketId}/comments");
        close.Content = JsonContent.Create(new { text = "", status = "Closed" });
        (await Client.SendAsync(close)).StatusCode.Should().Be(HttpStatusCode.OK);

        var written = await CommentAsTheGuest(token, "Уже не нужно?");

        written.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var data = await Track(token);
        data.GetProperty("canComment").GetBoolean().Should().BeFalse();
        data.GetProperty("comments").GetArrayLength().Should().Be(0);
        data.GetProperty("resolvedByUsername").GetString().Should().Be(TestConstants.AdminUserUsername);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    public async Task CreateTrackedTicketComment_AnswersAnUnknownTokenWithNotFound(string token)
    {
        var written = await CommentAsTheGuest(token, "Есть кто?");

        written.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateTicket_RefusesASuggestion(bool signedIn)
    {
        // Suggestions go to the forum. The category is kept for the queues that
        // filter by it, and the intake refuses it under the reason field.
        var body = new
        {
            subtype = "SiteImprovementSuggestion",
            text = "Сделайте темную тему",
            contact = "tester@example.com"
        };
        var request = signedIn
            ? CreateAuthenticatedRequest(HttpMethod.Post, "/v1/tickets")
            : new HttpRequestMessage(HttpMethod.Post, "/v1/tickets");
        request.Content = JsonContent.Create(body);

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        FieldsRefused(await response.Content.ReadAsStringAsync()).Should().Equal("Subtype");
    }

    [Fact]
    public async Task CreateTicket_RefusesHidingMarkup()
    {
        // A ticket renders with the tag set of profile texts, where a hiding
        // block is shown to nobody, moderation included.
        var response = await Client.PostAsJsonAsync("/v1/tickets", new
        {
            subtype = "Bug",
            text = "до [private=\"Чак\"]секрет[/private] после",
            contact = "tester@example.com"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        FieldsRefused(await response.Content.ReadAsStringAsync()).Should().Equal("Text");
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

    /// <summary>A ticket filed through the form by the test user, by its identifier.</summary>
    private async Task<Guid> FileAsTheTestUser(string text)
    {
        var post = CreateAuthenticatedRequest(HttpMethod.Post, "/v1/tickets");
        post.Content = JsonContent.Create(new { subtype = "Bug", text });
        var created = await Client.SendAsync(post);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        return JsonDocument.Parse(await created.Content.ReadAsStringAsync())
            .RootElement.GetProperty("ticketId").GetGuid();
    }

    /// <summary>A ticket filed through the form by a guest, with its tracking token.</summary>
    private async Task<(Guid TicketId, string Token)> FileAsAGuest(string text)
    {
        var created = await Client.PostAsJsonAsync("/v1/tickets", new
        {
            subtype = "Bug",
            text,
            contact = "tester@example.com"
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var answer = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement;
        return (answer.GetProperty("ticketId").GetGuid(), answer.GetProperty("trackingToken").GetString()!);
    }

    private Task<HttpResponseMessage> CommentAsTheGuest(string token, string text)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/tickets/track/comments");
        request.Headers.Add("X-Dm-Ticket-Token", token);
        request.Content = JsonContent.Create(new { text });
        return Client.SendAsync(request);
    }

    /// <summary>The tracking page of a guest ticket.</summary>
    private async Task<JsonElement> Track(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/tickets/track");
        request.Headers.Add("X-Dm-Ticket-Token", token);
        var tracked = await Client.SendAsync(request);
        var body = await tracked.Content.ReadAsStringAsync();
        tracked.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);
        return JsonDocument.Parse(body).RootElement.GetProperty("resource").Clone();
    }

    /// <summary>The field names a validation refusal names.</summary>
    private static IEnumerable<string> FieldsRefused(string body) =>
        JsonDocument.Parse(body).RootElement
            .GetProperty("errors")
            .EnumerateObject()
            .Select(field => field.Name);
}
