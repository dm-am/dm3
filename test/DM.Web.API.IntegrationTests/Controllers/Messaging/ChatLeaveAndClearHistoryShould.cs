using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.Messaging;

/// <summary>
/// The "…" menu of a correspondence: a participant leaves a group, and clears the
/// history of a conversation for himself while the others keep theirs.
/// </summary>
/// <remarks>
/// Written against HTTP rather than the services: the cleared history is a filter
/// in five queries (the messages of the chat, its last message, a message read by
/// its id, the search and the list), and every test with a substituted repository
/// steps straight over all of them.
///
/// Each case makes its own people and conversations and takes the conversations
/// back out; the people stay, the way the neighbouring suites leave theirs.
/// </remarks>
public class ChatLeaveAndClearHistoryShould : IntegrationTestBase
{
    /// <summary>A word only the messages of this suite carry, for the search.</summary>
    private const string SearchWord = "забытоеслово";

    public ChatLeaveAndClearHistoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// A participant leaves a group: it goes out of his list and every read of
    /// his, his unread count with it, and stays whole with the others.
    /// </summary>
    [Fact]
    public async Task TakeAGroupOutOfTheLeaversReadsAndLeaveItWholeToTheOthers()
    {
        var author = await SeedUserAsync("clh-aut");
        var leaver = await SeedUserAsync("clh-lvr");
        var third = await SeedUserAsync("clh-thr");
        var chats = new List<Guid>();
        try
        {
            var chatId = await CreateGroupAsync(author, chats, leaver, third);
            var messageId = await WriteAsync(author, chatId, "Первое сообщение группы");

            var asLeaver = await ReadAsync(leaver, HttpMethod.Get, $"/v1/chats/{chatId}");
            Viewer(asLeaver, "canLeave").Should().BeTrue("a participant of a group may leave it");
            Viewer(asLeaver, "canClearHistory").Should().BeTrue();
            (await UnreadAsync(leaver)).Should().Be(1, "the author's message is unread for him");

            (await SendAsync(leaver, HttpMethod.Post, $"/v1/chats/{chatId}/leave"))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            (await ListedAsync(leaver)).Should().NotContain(chatId);
            (await SendAsync(leaver, HttpMethod.Get, $"/v1/chats/{chatId}"))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await SendAsync(leaver, HttpMethod.Get, $"/v1/chats/{chatId}/messages"))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await SendAsync(leaver, HttpMethod.Get, $"/v1/messages/{messageId}"))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await SendAsync(leaver, HttpMethod.Post, $"/v1/chats/{chatId}/messages", Text("Я еще здесь?")))
                .StatusCode.Should().Be(HttpStatusCode.NotFound, "he is no longer a participant to write");
            (await UnreadAsync(leaver)).Should().Be(0, "his marker went with him");
            (await SendAsync(leaver, HttpMethod.Post, $"/v1/chats/{chatId}/leave"))
                .StatusCode.Should().Be(HttpStatusCode.NotFound, "there is nothing left to leave");

            var asAuthor = await ReadAsync(author, HttpMethod.Get, $"/v1/chats/{chatId}");
            ParticipantIds(asAuthor).Should().BeEquivalentTo(new[] { author.UserId, third.UserId });
            asAuthor.GetProperty("lastMessage").GetProperty("id").GetGuid().Should().Be(messageId);
            (await MessageIdsAsync(third, chatId)).Should().Equal(messageId);
            (await ListedAsync(third)).Should().Contain(chatId);
        }
        finally
        {
            await DropAsync(chats);
        }
    }

    /// <summary>
    /// A direct chat is not left: there would be nobody to stay in it. The flag
    /// says so before the refusal does.
    /// </summary>
    [Fact]
    public async Task NotLeaveADirectChat()
    {
        var one = await SeedUserAsync("clh-one");
        var other = await SeedUserAsync("clh-oth");
        var chats = new List<Guid>();
        try
        {
            var chat = await ReadAsync(one, HttpMethod.Post, $"/v1/chats/direct/{other.Username}");
            var chatId = chat.GetProperty("id").GetGuid();
            chats.Add(chatId);
            Viewer(chat, "canLeave").Should().BeFalse();
            Viewer(chat, "canClearHistory").Should().BeTrue();

            (await SendAsync(one, HttpMethod.Post, $"/v1/chats/{chatId}/leave"))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);

            ParticipantIds(await ReadAsync(one, HttpMethod.Get, $"/v1/chats/{chatId}"))
                .Should().BeEquivalentTo(new[] { one.UserId, other.UserId });
        }
        finally
        {
            await DropAsync(chats);
        }
    }

    /// <summary>
    /// The global chat is read by everybody and belongs to nobody: there is no
    /// participant to leave it or to keep a cleared history in it.
    /// </summary>
    [Fact]
    public async Task NeitherLeaveNorClearTheGlobalChat()
    {
        var reader = await SeedUserAsync("clh-glb");

        var chat = await ReadAsync(reader, HttpMethod.Get, "/v1/chats/global");
        Viewer(chat, "canLeave").Should().BeFalse();
        Viewer(chat, "canClearHistory").Should().BeFalse();

        (await SendAsync(reader, HttpMethod.Post, "/v1/chats/global/leave"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(reader, HttpMethod.Post, "/v1/chats/global/clear-history"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Somebody else's conversation can be neither left nor cleared, by either of
    /// its addresses, and it answers the stranger the way its every read does:
    /// not found. A guest is not let in at all.
    /// </summary>
    [Fact]
    public async Task RefuseAStrangerToLeaveOrClearSomebodyElsesConversation()
    {
        var author = await SeedUserAsync("clh-aut");
        var member = await SeedUserAsync("clh-mbr");
        var stranger = await SeedUserAsync("clh-str");
        var chats = new List<Guid>();
        try
        {
            var chatId = await CreateGroupAsync(author, chats, member);
            var messageId = await WriteAsync(author, chatId, "Только для своих");
            var publicId = await PublicIdAsync(chatId);

            foreach (var address in new[] { chatId.ToString(), publicId })
            {
                (await SendAsync(stranger, HttpMethod.Post, $"/v1/chats/{address}/leave"))
                    .StatusCode.Should().Be(HttpStatusCode.NotFound, address);
                (await SendAsync(stranger, HttpMethod.Post, $"/v1/chats/{address}/clear-history"))
                    .StatusCode.Should().Be(HttpStatusCode.NotFound, address);
                (await SendAsync(null, HttpMethod.Post, $"/v1/chats/{address}/leave"))
                    .StatusCode.Should().Be(HttpStatusCode.Unauthorized, address);
                (await SendAsync(null, HttpMethod.Post, $"/v1/chats/{address}/clear-history"))
                    .StatusCode.Should().Be(HttpStatusCode.Unauthorized, address);
            }

            ParticipantIds(await ReadAsync(member, HttpMethod.Get, $"/v1/chats/{chatId}"))
                .Should().BeEquivalentTo(new[] { author.UserId, member.UserId });
            (await MessageIdsAsync(member, chatId)).Should().Equal(messageId);
            (await MessageIdsAsync(author, chatId)).Should().Equal(messageId);
        }
        finally
        {
            await DropAsync(chats);
        }
    }

    /// <summary>
    /// The history is cleared for the one who asked: none of the ways of reading
    /// the conversation gives him what was written up to that moment, the other
    /// participant keeps all of it, and what is written afterwards reaches both.
    /// </summary>
    [Fact]
    public async Task ClearTheHistoryForTheCallerAndKeepItForTheOther()
    {
        var clearer = await SeedUserAsync("clh-clr");
        var other = await SeedUserAsync("clh-oth");
        var chats = new List<Guid>();
        try
        {
            var chatId = (await ReadAsync(clearer, HttpMethod.Post, $"/v1/chats/direct/{other.Username}"))
                .GetProperty("id").GetGuid();
            chats.Add(chatId);
            var own = await WriteAsync(clearer, chatId, $"Мое старое сообщение, {SearchWord}");
            var answer = await WriteAsync(other, chatId, $"Ответ на него, {SearchWord}");
            (await UnreadAsync(clearer)).Should().Be(1, "the answer is unread for him");

            (await SendAsync(clearer, HttpMethod.Post, $"/v1/chats/{chatId}/clear-history"))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            // The clearer: none of it, by any way of reading.
            (await MessageIdsAsync(clearer, chatId)).Should().BeEmpty();
            (await MessageIdsAsync(clearer, chatId, $"aroundMessageId={own}")).Should().BeEmpty();
            (await MessageIdsAsync(clearer, chatId,
                    $"nearTimestampUtc={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"))}"))
                .Should().BeEmpty();
            (await SendAsync(clearer, HttpMethod.Get, $"/v1/messages/{own}"))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await SendAsync(clearer, HttpMethod.Get, $"/v1/messages/{answer}"))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
            HasLastMessage(await ReadAsync(clearer, HttpMethod.Get, $"/v1/chats/{chatId}")).Should().BeFalse();
            var listed = await ListedChatAsync(clearer, chatId);
            HasLastMessage(listed).Should().BeFalse("the list shows the chat, and nothing of what he cleared");
            (await UnreadAsync(clearer)).Should().Be(0, "nothing is left for him to read");
            (await SearchIdsAsync(clearer, chatId)).Should().BeEmpty();

            // The other participant: all of it, untouched.
            (await MessageIdsAsync(other, chatId)).Should().Equal(own, answer);
            (await ReadAsync(other, HttpMethod.Get, $"/v1/chats/{chatId}"))
                .GetProperty("lastMessage").GetProperty("id").GetGuid().Should().Be(answer);
            (await SearchIdsAsync(other, chatId)).Should().BeEquivalentTo(new[] { own, answer });

            // Written afterwards: both of them see it.
            var fresh = await WriteAsync(other, chatId, $"Новое после очистки, {SearchWord}");
            (await MessageIdsAsync(clearer, chatId)).Should().Equal(fresh);
            (await ReadAsync(clearer, HttpMethod.Get, $"/v1/chats/{chatId}"))
                .GetProperty("lastMessage").GetProperty("id").GetGuid().Should().Be(fresh);
            (await SearchIdsAsync(clearer, chatId)).Should().Equal(fresh);
            (await UnreadAsync(clearer)).Should().Be(1);
            (await MessageIdsAsync(other, chatId)).Should().Equal(own, answer, fresh);
        }
        finally
        {
            await DropAsync(chats);
        }
    }

    // ═══ READS ═══

    private static bool Viewer(JsonElement chat, string flag) =>
        chat.GetProperty("viewer").GetProperty(flag).GetBoolean();

    private static Guid[] ParticipantIds(JsonElement chat) => chat.GetProperty("participants")
        .EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToArray();

    private static bool HasLastMessage(JsonElement chat) =>
        chat.TryGetProperty("lastMessage", out var last) && last.ValueKind != JsonValueKind.Null;

    private async Task<Guid[]> ListedAsync(GeneralUser user) =>
        (await ReadAsync(user, HttpMethod.Get, "/v1/chats")).GetProperty("resources")
        .EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToArray();

    private async Task<JsonElement> ListedChatAsync(GeneralUser user, Guid chatId) =>
        (await ReadAsync(user, HttpMethod.Get, "/v1/chats")).GetProperty("resources")
        .EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == chatId);

    private async Task<Guid[]> MessageIdsAsync(GeneralUser user, Guid chatId, string? query = null) =>
        (await ReadAsync(user, HttpMethod.Get, $"/v1/chats/{chatId}/messages?limit=100&{query}"))
        .GetProperty("resources").EnumerateArray().Select(m => m.GetProperty("id").GetGuid()).ToArray();

    private async Task<Guid[]> SearchIdsAsync(GeneralUser user, Guid chatId) =>
        (await ReadAsync(user, HttpMethod.Get, $"/v1/search/messages?search={SearchWord}&in=dm:{chatId}"))
        .GetProperty("resources").EnumerateArray().Select(h => h.GetProperty("id").GetGuid()).ToArray();

    private async Task<long> UnreadAsync(GeneralUser user) =>
        (await ReadAsync(user, HttpMethod.Get, "/v1/chats/unread"))
        .GetProperty("resource").GetProperty("count").GetInt64();

    // ═══ WRITES ═══

    private static object Text(string text) => new { text };

    private async Task<Guid> CreateGroupAsync(GeneralUser author, List<Guid> chats, params GeneralUser[] others)
    {
        var chatId = (await ReadAsync(author, HttpMethod.Post, "/v1/chats", new
        {
            title = "Группа для меню переписки",
            participantIds = others.Select(o => o.UserId).ToArray()
        })).GetProperty("id").GetGuid();
        chats.Add(chatId);
        return chatId;
    }

    private async Task<Guid> WriteAsync(GeneralUser author, Guid chatId, string text) =>
        (await ReadAsync(author, HttpMethod.Post, $"/v1/chats/{chatId}/messages", Text(text)))
        .GetProperty("resource").GetProperty("id").GetGuid();

    // ═══ SEEDING ═══

    private async Task<GeneralUser> SeedUserAsync(string prefix)
    {
        var userId = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"{prefix}{userId:N}"[..20],
            Email = $"{userId:N}@chatleave.example",
            PasswordHash = "hash",
            Salt = "salt"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new GeneralUser { UserId = userId, Username = user.Username, Role = UserRole.RegularUser };
    }

    private async Task<string> PublicIdAsync(Guid chatId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        return (await db.Chats.Where(c => c.ChatId == chatId).Select(c => c.PublicId).SingleAsync())!;
    }

    /// <summary>The conversations of the case, their messages, markers and links.</summary>
    private async Task DropAsync(IReadOnlyCollection<Guid> chats)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.Chats.Where(c => chats.Contains(c.ChatId))
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.LastMessageId, (Guid?)null));
        await db.Set<DbMessage>().IgnoreQueryFilters().Where(m => chats.Contains(m.ChatId)).ExecuteDeleteAsync();
        await db.UnreadCounters.Where(c => chats.Contains(c.EntityId)).ExecuteDeleteAsync();
        await db.UserChatLinks.IgnoreQueryFilters().Where(l => chats.Contains(l.ChatId)).ExecuteDeleteAsync();
        await db.Chats.Where(c => chats.Contains(c.ChatId)).ExecuteDeleteAsync();
    }

    // ═══ REQUESTS ═══

    /// <summary>A request as the user; a guest sends none of the test headers.</summary>
    private Task<HttpResponseMessage> SendAsync(
        GeneralUser? user, HttpMethod method, string url, object? body = null)
    {
        var request = user is null
            ? new HttpRequestMessage(method, url)
            : CreateAuthenticatedRequest(method, url, user);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return Client.SendAsync(request);
    }

    /// <summary>A successful answer, read whole.</summary>
    private async Task<JsonElement> ReadAsync(
        GeneralUser user, HttpMethod method, string url, object? body = null)
    {
        var response = await SendAsync(user, method, url, body);
        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"{method} {url} answered {response.StatusCode}: {text}");
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }
}
