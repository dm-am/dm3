using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Messaging;

/// <summary>
/// A single reply of the global chat is read, edited, liked and deleted through
/// the routes of the global chat itself.
/// </summary>
/// <remarks>
/// It used to be addressed through <c>/v1/messages/{id}</c>, the routes of
/// private correspondence, whose read asks whether the reader is a participant
/// of the chat. The global chat has no participants — the right to read it
/// belongs to everyone by definition — so that question answered "no" for
/// everybody, the author of the message included, and every one of these
/// operations answered 404 on the reader's own line.
///
/// Written against the HTTP surface rather than the services: the refusal lived
/// in a query predicate, which every test with a substituted repository steps
/// straight over.
/// </remarks>
public class GlobalChatMessageOperationsShould : IntegrationTestBase
{
    public GlobalChatMessageOperationsShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static GeneralUser Author => CustomWebApplicationFactory.CreateTestUser();
    private static GeneralUser Reader => CustomWebApplicationFactory.CreateSecondUser();
    private static GeneralUser Moderator => CustomWebApplicationFactory.CreateModeratorUser();

    /// <summary>Sends a line to the global chat and returns its identifier.</summary>
    private async Task<Guid> Say(GeneralUser author, string text)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/v1/global-chat/messages", author);
        request.Content = JsonContent.Create(new { text });

        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "sending is the one operation that already had a route of its own");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("resource").GetProperty("id").GetGuid();
    }

    private static async Task<string> TextOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("resource").GetProperty("text").GetString()!;
    }

    /// <summary>Names on the likes of the single reply an answer carries.</summary>
    private static async Task<string[]> LikersOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return Names(document.RootElement.GetProperty("resource"));
    }

    /// <summary>Names on the likes of one reply of a listed page.</summary>
    private static async Task<string[]> LikersOfListed(HttpResponseMessage response, Guid messageId)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var listed in document.RootElement.GetProperty("resources").EnumerateArray())
        {
            if (listed.GetProperty("id").GetGuid() == messageId)
            {
                return Names(listed);
            }
        }

        throw new Xunit.Sdk.XunitException($"The listing does not carry message {messageId}");
    }

    private static string[] Names(JsonElement message) => message
        .GetProperty("likes")
        .EnumerateArray()
        .Select(l => l.GetProperty("username").GetString()!)
        .ToArray();

    [Fact]
    public async Task ReadOneReply()
    {
        var messageId = await Say(Author, "Одна реплика общего чата");

        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/global-chat/messages/{messageId}", Author));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TextOf(response)).Should().Contain("Одна реплика общего чата");
    }

    /// <summary>
    /// Reading the global chat needs no account, and one line of it is part of
    /// the same page.
    /// </summary>
    [Fact]
    public async Task ReadOneReplyWithoutAnAccount()
    {
        var messageId = await Say(Author, "Видно и без входа");

        var response = await Client.GetAsync($"/v1/global-chat/messages/{messageId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The author asks for their own line the way the editor opens it: with the
    /// audience header that returns round-trip markup instead of the display
    /// render.
    /// </summary>
    /// <remarks>
    /// Told apart by [mod], which is the one tag of this surface the two renders
    /// spell differently — the round-trip one carries <c>data-bb-tag</c> so the
    /// editor can turn the markup back into BBCode. Authored by a moderator
    /// because a regular author's [mod] is stripped on the way in.
    /// </remarks>
    [Fact]
    public async Task HandTheAuthorTheRoundTripMarkupOfTheirOwnReply()
    {
        var messageId = await Say(Moderator, "[mod]Объявление[/mod]");

        var forEditing = CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/global-chat/messages/{messageId}", Moderator);
        forEditing.Headers.Add("X-Dm-Audience", "author_edit");
        var response = await Client.SendAsync(forEditing);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TextOf(response)).Should().Contain("data-bb-tag=\"mod\"",
            "the editor is filled with markup it can turn back into BBCode");

        var display = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/global-chat/messages/{messageId}", Reader));
        (await TextOf(display)).Should().NotContain("data-bb-tag",
            "the round-trip markup is the author's own view, and reading it is the ordinary render");
    }

    [Fact]
    public async Task LetTheAuthorEditTheirOwnReply()
    {
        var messageId = await Say(Author, "Первая редакция");

        var request = CreateAuthenticatedRequest(
            HttpMethod.Patch, $"/v1/global-chat/messages/{messageId}", Author);
        request.Content = JsonContent.Create(new { text = "Вторая редакция" });
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TextOf(response)).Should().Contain("Вторая редакция");
    }

    /// <summary>
    /// A second like of the same line is refused as a duplicate, and only a like
    /// that exists can be duplicated. What the answer carries is the subject of
    /// <see cref="HandBackTheReplyCarryingTheLikeJustLeft" /> below.
    /// </summary>
    [Fact]
    public async Task LetAReaderLikeSomebodyElsesReply()
    {
        var messageId = await Say(Author, "Реплика, которую лайкают");

        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Post, $"/v1/global-chat/messages/{messageId}/likes", Reader));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var again = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Post, $"/v1/global-chat/messages/{messageId}/likes", Reader));
        again.StatusCode.Should().Be(HttpStatusCode.Conflict, "the like landed and is still there");
    }

    /// <summary>
    /// Taking back a like that was never left is refused, so the 204 below is
    /// itself the proof that the like was there to take back.
    /// </summary>
    [Fact]
    public async Task LetAReaderTakeTheirLikeBack()
    {
        var messageId = await Say(Author, "Реплика, с которой снимают лайк");
        var liked = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Post, $"/v1/global-chat/messages/{messageId}/likes", Reader));
        liked.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Delete, $"/v1/global-chat/messages/{messageId}/likes", Reader));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var again = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Delete, $"/v1/global-chat/messages/{messageId}/likes", Reader));
        again.StatusCode.Should().Be(HttpStatusCode.Conflict, "there is nothing left to take back");
    }

    /// <summary>
    /// The answer to the like carries the line it was left on, and that line
    /// carries the like.
    /// </summary>
    /// <remarks>
    /// This is what the reader sees: the client replaces the message it has with
    /// the one the answer brings back, so a like missing from the answer puts the
    /// heart out again the instant it was clicked.
    /// </remarks>
    [Fact]
    public async Task HandBackTheReplyCarryingTheLikeJustLeft()
    {
        var messageId = await Say(Author, "Реплика, чей лайк возвращается в ответе");

        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Post, $"/v1/global-chat/messages/{messageId}/likes", Reader));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LikersOf(response)).Should().Contain(Reader.Username,
            "the answer replaces the message on the page, and the like has to be on it");
    }

    /// <summary>
    /// A like left earlier is on the line when the page is read again.
    /// </summary>
    [Fact]
    public async Task ShowAStandingLikeOnTheListedReply()
    {
        var messageId = await Say(Author, "Реплика с лайком, которую перечитывают");
        var liked = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Post, $"/v1/global-chat/messages/{messageId}/likes", Reader));
        liked.StatusCode.Should().Be(HttpStatusCode.OK);

        var listing = await Client.GetAsync("/v1/global-chat/messages?limit=100");

        listing.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LikersOfListed(listing, messageId)).Should().Contain(Reader.Username);
    }

    /// <summary>
    /// And on the single read of that same line.
    /// </summary>
    [Fact]
    public async Task ShowAStandingLikeOnTheReplyReadOnItsOwn()
    {
        var messageId = await Say(Author, "Реплика с лайком, которую читают отдельно");
        var liked = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Post, $"/v1/global-chat/messages/{messageId}/likes", Reader));
        liked.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await Client.GetAsync($"/v1/global-chat/messages/{messageId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LikersOf(response)).Should().Contain(Reader.Username);
    }

    /// <summary>
    /// A line nobody liked carries an empty list rather than a name.
    /// </summary>
    [Fact]
    public async Task LeaveTheLikesOfAnUnlikedReplyEmpty()
    {
        var messageId = await Say(Author, "Реплика, которую никто не лайкал");

        var response = await Client.GetAsync($"/v1/global-chat/messages/{messageId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LikersOf(response)).Should().BeEmpty();
    }

    /// <summary>
    /// Nobody likes their own line, on the global chat as anywhere else.
    /// </summary>
    [Fact]
    public async Task RefuseTheAuthorTheLikeOfTheirOwnReply()
    {
        var messageId = await Say(Author, "Своя реплика");

        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Post, $"/v1/global-chat/messages/{messageId}/likes", Author));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LetTheAuthorDeleteTheirOwnReply()
    {
        var messageId = await Say(Author, "Реплика, которую удалит автор");

        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Delete, $"/v1/global-chat/messages/{messageId}", Author));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterwards = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/global-chat/messages/{messageId}", Author));
        afterwards.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a deleted line is gone from the read, the way it is in a private chat");
    }

    /// <summary>
    /// Somebody else's line is not an ordinary reader's to rewrite. The rule is
    /// <c>MessageIntention.Edit</c>, the one the private-message route has always
    /// applied.
    /// </summary>
    [Fact]
    public async Task RefuseAnOrdinaryReaderTheEditOfSomebodyElsesReply()
    {
        var messageId = await Say(Author, "Чужая реплика");

        var request = CreateAuthenticatedRequest(
            HttpMethod.Patch, $"/v1/global-chat/messages/{messageId}", Reader);
        request.Content = JsonContent.Create(new { text = "Переписал за автора" });
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RefuseAnOrdinaryReaderTheDeleteOfSomebodyElsesReply()
    {
        var messageId = await Say(Author, "Чужая реплика, которую не удалить");

        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Delete, $"/v1/global-chat/messages/{messageId}", Reader));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// And moderation deletes somebody else's line by the same rule it deletes a
    /// private one by: <c>MessageIntentionResolver.CanEditOrDelete</c> lets
    /// Moderator and above through without asking about authorship or about the
    /// editing window.
    /// </summary>
    [Fact]
    public async Task LetModerationDeleteSomebodyElsesReply()
    {
        var messageId = await Say(Author, "Реплика, которую удалит модератор");

        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Delete, $"/v1/global-chat/messages/{messageId}", Moderator));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Quoting a line of the global chat is read the same way the line is: the
    /// markup of the quotation comes from the route of the global chat, not from
    /// the one that asks whether the reader is a participant of the chat.
    /// </summary>
    [Fact]
    public async Task HandOutTheQuotationOfAReply()
    {
        var messageId = await Say(Author, "Реплика, которую цитируют");

        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/global-chat/messages/{messageId}/quote", Reader));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var quote = await TextOf(response);
        quote.Should().Contain("Реплика, которую цитируют");
        quote.Should().Contain(Author.Username, "the quotation is attributed to the author of the line");
    }

    /// <summary>
    /// Reading the global chat needs no account, and quoting one of its lines is
    /// the same read.
    /// </summary>
    [Fact]
    public async Task HandOutTheQuotationOfAReplyWithoutAnAccount()
    {
        var messageId = await Say(Author, "Цитируется и без входа");

        var response = await Client.GetAsync($"/v1/global-chat/messages/{messageId}/quote");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// And the quotation route accepts nothing but global chat lines, the way its
    /// six neighbours do.
    /// </summary>
    [Fact]
    public async Task RefuseAPrivateMessageOnTheGlobalChatQuoteRoute()
    {
        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/global-chat/messages/{TestConstants.TestMessageId}/quote", Author));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A private message keeps answering on its own routes. These are neighbours
    /// of the message routes, not a move of them.
    /// </summary>
    [Fact]
    public async Task LeaveAPrivateMessageOnItsOwnRoute()
    {
        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/messages/{TestConstants.TestMessageId}", Author));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// And the global chat routes accept nothing but global chat lines: a private
    /// message addressed through them is not found, whoever asks.
    /// </summary>
    [Fact]
    public async Task RefuseAPrivateMessageOnTheGlobalChatRoute()
    {
        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/global-chat/messages/{TestConstants.TestMessageId}", Author));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
