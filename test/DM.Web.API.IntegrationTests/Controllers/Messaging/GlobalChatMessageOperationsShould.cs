using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Messaging;

// The global chat has no participants, so the private-correspondence routes refuse everybody.
public class GlobalChatMessageOperationsShould : IntegrationTestBase
{
    public GlobalChatMessageOperationsShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static GeneralUser Author => CustomWebApplicationFactory.CreateTestUser();
    private static GeneralUser Reader => CustomWebApplicationFactory.CreateSecondUser();
    private static GeneralUser Moderator => CustomWebApplicationFactory.CreateModeratorUser();

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

    private static async Task<string[]> LikersOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return Names(document.RootElement.GetProperty("resource"));
    }

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
        var messageId = await Say(Author, "Одна реплика глобального чата");

        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/global-chat/messages/{messageId}", Author));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TextOf(response)).Should().Contain("Одна реплика глобального чата");
    }

    // Reading the global chat needs no account, and one line of it is part of the same page.
    [Fact]
    public async Task ReadOneReplyWithoutAnAccount()
    {
        var messageId = await Say(Author, "Видно и без входа");

        var response = await Client.GetAsync($"/v1/global-chat/messages/{messageId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Authored by a moderator, whose [mod] stays.
    [Fact]
    public async Task HandTheAuthorTheSourceOfTheirOwnReply()
    {
        var messageId = await Say(Moderator, "[mod]Объявление[/mod]");

        var forEditing = CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/global-chat/messages/{messageId}", Moderator);
        forEditing.Headers.Add("X-Dm-Audience", "author_edit");
        var response = await Client.SendAsync(forEditing);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TextOf(response)).Should().Be("[mod]Объявление[/mod]",
            "the editor works on BBCode and is filled with the text as typed");

        var display = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/global-chat/messages/{messageId}", Reader));
        (await TextOf(display)).Should().Contain("mod-block").And.NotContain("[mod]",
            "the source is the author's own view, and reading it is the ordinary render");
    }

    [Fact]
    public async Task HandAModeratorTheSourceOfSomebodyElsesReply()
    {
        var messageId = await Say(Author, "[b]Реплика[/b] для правки");

        var forEditing = CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/global-chat/messages/{messageId}", Moderator);
        forEditing.Headers.Add("X-Dm-Audience", "author_edit");
        (await TextOf(await Client.SendAsync(forEditing))).Should().Be("[b]Реплика[/b] для правки");

        var byReader = CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/global-chat/messages/{messageId}", Reader);
        byReader.Headers.Add("X-Dm-Audience", "author_edit");
        (await TextOf(await Client.SendAsync(byReader))).Should().Contain("<strong>").And.NotContain("[b]");
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

    // Taking back a like that was never left is refused, so the 204 below is itself the proof that
    // the like was there to take back.
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

    // The client replaces its message with the one the answer brings.
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

    // Moderation deletes somebody else's line: MessageIntentionResolver.CanEditOrDelete lets
    // Moderator and above through.
    [Fact]
    public async Task LetModerationDeleteSomebodyElsesReply()
    {
        var messageId = await Say(Author, "Реплика, которую удалит модератор");

        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Delete, $"/v1/global-chat/messages/{messageId}", Moderator));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
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

    [Fact]
    public async Task RefuseAPrivateMessageOnTheGlobalChatRoute()
    {
        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/global-chat/messages/{TestConstants.TestMessageId}", Author));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
