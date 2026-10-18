using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Chats;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbUserBlacklist = DM.Infrastructure.Persistence.Entities.Account.UserBlacklist;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// The profile offers "Написать сообщение" by <c>viewer.canMessage</c>, the page
/// of a correspondence offers its field by <c>viewer.canWrite</c> of the chat, and
/// each flag is false exactly when the message it stands for is refused.
/// </summary>
/// <remarks>
/// The owner, 05.10: what the user may not do is not offered, rather than
/// refused after it was typed. The profile gave the button to everybody signed
/// in, the correspondence gave the field to every participant, and a person kept
/// on the addressee's blacklist with private messages blocked wrote the message
/// and was told after it "Нельзя отправить сообщение этому пользователю".
///
/// Every row reads the flag the way the page reads it and then sends the message
/// the page would send, as the same viewer, so the two answers are compared
/// rather than each taken on trust. The expected value is asserted too: a flag
/// and a write that both went wrong the same way would agree with each other.
///
/// Each case makes its own people and conversations and takes the conversations
/// back out; the people stay, the way the neighbouring suites leave theirs.
/// </remarks>
public class CanMessageFlagShould : IntegrationTestBase
{
    public CanMessageFlagShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>What the addressee's blacklist holds about the writer.</summary>
    public enum Block
    {
        None,

        /// <summary>The writer is on it, and the list closes private messages.</summary>
        WithPrivateMessagesClosed,

        /// <summary>The writer is on it, and the list leaves private messages open.</summary>
        WithoutTheSwitch
    }

    /// <summary>The conversation the field is asked about.</summary>
    public enum Conversation
    {
        Direct,
        GroupOfTwo,
        GroupOfThree
    }

    /// <summary>
    /// "Написать сообщение" leads to the conversation of the two, and the button
    /// is there exactly when its first message is taken.
    /// </summary>
    [Theory]
    [InlineData(Block.None, true)]
    [InlineData(Block.WithPrivateMessagesClosed, false)]
    [InlineData(Block.WithoutTheSwitch, true)]
    public async Task OfferTheMessageOnAProfileExactlyWhenTheMessageIsTaken(Block block, bool expected)
    {
        var addressee = await SeedUserAsync("cmf-adr", SettingsOf(block));
        var writer = await SeedUserAsync("cmf-wrt");
        var chats = new List<Guid>();
        try
        {
            if (block != Block.None)
            {
                await BlockAsync(addressee, writer);
            }

            var flag = (await ProfileAsync(writer, addressee.Username))
                .GetProperty("viewer").GetProperty("canMessage").GetBoolean();

            // The way the button leads: the conversation of the two, opened by
            // name, and its first message.
            var chat = await ReadAsync(writer, HttpMethod.Post, $"/v1/chats/direct/{addressee.Username}");
            var chatId = chat.GetProperty("id").GetGuid();
            chats.Add(chatId);
            var sent = await SendAsync(writer, HttpMethod.Post, $"/v1/chats/{chatId}/messages", Text);
            var body = await sent.Content.ReadAsStringAsync();

            flag.Should().Be(expected, block.ToString());
            sent.IsSuccessStatusCode.Should().Be(flag, $"the flag said {flag} and the message answered: {body}");
            if (!flag)
            {
                sent.StatusCode.Should().Be(HttpStatusCode.Forbidden, body);
            }

            // The conversation it leads to offers its field by the same answer.
            CanWrite(chat).Should().Be(flag);
        }
        finally
        {
            await DropAsync(chats, writer, addressee);
        }
    }

    /// <summary>
    /// A guest is offered no message: the conversation is opened for a signed-in
    /// reader only. Nobody is offered one on his own profile, and the profile of
    /// "Аккаунт удален" is nobody's to write to and answers no flag at all.
    /// </summary>
    [Fact]
    public async Task OfferNoMessageToAGuestOnOnesOwnProfileOrToTheRemovedAccount()
    {
        var addressee = await SeedUserAsync("cmf-adr");
        try
        {
            (await ProfileAsync(null, addressee.Username))
                .GetProperty("viewer").GetProperty("canMessage").GetBoolean().Should().BeFalse();
            (await SendAsync(null, HttpMethod.Post, $"/v1/chats/direct/{addressee.Username}"))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            (await ProfileAsync(addressee, addressee.Username))
                .GetProperty("viewer").GetProperty("canMessage").GetBoolean().Should().BeFalse();

            var removed = await ProfileAsync(addressee, RemovedAccount.Name);
            (removed.TryGetProperty("viewer", out var viewer) && viewer.ValueKind != JsonValueKind.Null)
                .Should().BeFalse();
        }
        finally
        {
            await DropAsync([], addressee);
        }
    }

    /// <summary>
    /// The field of a correspondence is there exactly when its message is taken:
    /// the one other participant of a conversation of two answers for it, whatever
    /// type the conversation was made under, and a group of three is no private
    /// correspondence.
    /// </summary>
    [Theory]
    [InlineData(Conversation.Direct, Block.None, true)]
    [InlineData(Conversation.Direct, Block.WithPrivateMessagesClosed, false)]
    [InlineData(Conversation.Direct, Block.WithoutTheSwitch, true)]
    [InlineData(Conversation.GroupOfTwo, Block.WithPrivateMessagesClosed, false)]
    [InlineData(Conversation.GroupOfThree, Block.WithPrivateMessagesClosed, true)]
    public async Task OfferTheFieldOfACorrespondenceExactlyWhenTheMessageIsTaken(
        Conversation shape, Block block, bool expected)
    {
        var addressee = await SeedUserAsync("cmf-adr", SettingsOf(block));
        var writer = await SeedUserAsync("cmf-wrt");
        var third = await SeedUserAsync("cmf-thr");
        var chats = new List<Guid>();
        try
        {
            var chatId = shape == Conversation.Direct
                ? (await ReadAsync(writer, HttpMethod.Post, $"/v1/chats/direct/{addressee.Username}"))
                .GetProperty("id").GetGuid()
                : (await ReadAsync(writer, HttpMethod.Post, "/v1/chats", new
                {
                    title = "Переписка для поля",
                    participantIds = shape == Conversation.GroupOfTwo
                        ? new[] { addressee.UserId }
                        : new[] { addressee.UserId, third.UserId }
                })).GetProperty("id").GetGuid();
            chats.Add(chatId);

            // After the conversation is made: the door of a group refuses a
            // person who blocked its author, and the case is about the list
            // that came after.
            if (block != Block.None)
            {
                await BlockAsync(addressee, writer);
            }

            var flag = CanWrite(await ReadAsync(writer, HttpMethod.Get, $"/v1/chats/{chatId}"));
            // The readable id is the other address of the same read.
            (CanWrite(await ReadAsync(writer, HttpMethod.Get, $"/v1/chats/{await PublicIdAsync(chatId)}")))
                .Should().Be(flag, "both addresses of one chat answer alike");

            var sent = await SendAsync(writer, HttpMethod.Post, $"/v1/chats/{chatId}/messages", Text);
            var body = await sent.Content.ReadAsStringAsync();

            flag.Should().Be(expected, $"{shape}, {block}");
            sent.IsSuccessStatusCode.Should().Be(flag, $"the flag said {flag} and the message answered: {body}");
            if (!flag)
            {
                sent.StatusCode.Should().Be(HttpStatusCode.Forbidden, body);
            }
        }
        finally
        {
            await DropAsync(chats, writer, addressee, third);
        }
    }

    /// <summary>
    /// The global chat is a chat read here too, and its flag says what its POST
    /// says: a ban on public speech closes it, the private correspondence of the
    /// same reader stays open.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task TellTheTruthAboutTheGlobalChat(bool banned, bool expected)
    {
        var writer = await SeedUserAsync("cmf-wrt");
        var addressee = await SeedUserAsync("cmf-adr");
        var chats = new List<Guid>();
        var lastBefore = await GlobalLastMessageAsync();
        try
        {
            var flag = CanWrite(await ReadAsync(writer, HttpMethod.Get, "/v1/chats/global", banned: banned));
            var sent = await SendAsync(writer, HttpMethod.Post, "/v1/global-chat/messages", Text, banned);
            var body = await sent.Content.ReadAsStringAsync();

            flag.Should().Be(expected);
            sent.IsSuccessStatusCode.Should().Be(flag, $"the flag said {flag} and the message answered: {body}");
            if (!flag)
            {
                sent.StatusCode.Should().Be(HttpStatusCode.Forbidden, body);
            }

            // Private correspondence is deliberately outside the ordinary ban.
            (await ProfileAsync(writer, addressee.Username, banned))
                .GetProperty("viewer").GetProperty("canMessage").GetBoolean().Should().BeTrue();
            var direct = await ReadAsync(writer, HttpMethod.Post, $"/v1/chats/direct/{addressee.Username}",
                banned: banned);
            chats.Add(direct.GetProperty("id").GetGuid());
            CanWrite(direct).Should().BeTrue();
        }
        finally
        {
            await DropGlobalMessagesAsync(writer, lastBefore);
            await DropAsync(chats, writer, addressee);
        }
    }

    /// <summary>
    /// The writes that hand a chat back carry the flag too, the page may put
    /// their answer in place of the chat it holds; the list of chats carries
    /// none, nothing is written from it.
    /// </summary>
    [Fact]
    public async Task AnswerTheWritesOfAChatWithTheFlagAndLeaveItOffTheList()
    {
        var writer = await SeedUserAsync("cmf-wrt");
        var addressee = await SeedUserAsync("cmf-adr");
        var third = await SeedUserAsync("cmf-thr");
        var chats = new List<Guid>();
        try
        {
            var created = await ReadAsync(writer, HttpMethod.Post, "/v1/chats", new
            {
                title = "Группа для признака",
                participantIds = new[] { addressee.UserId, third.UserId }
            });
            var chatId = created.GetProperty("id").GetGuid();
            chats.Add(chatId);
            CanWrite(created).Should().BeTrue();
            // The list shows a conversation somebody has written in.
            (await SendAsync(writer, HttpMethod.Post, $"/v1/chats/{chatId}/messages", Text))
                .IsSuccessStatusCode.Should().BeTrue();

            await BlockAsync(addressee, writer);
            // The third leaves, and the group is a conversation of two whose other
            // participant closed private messages to the writer.
            var updated = await ReadAsync(writer, HttpMethod.Patch, $"/v1/chats/{chatId}", new
            {
                removeParticipants = new[] { third.UserId }
            });
            CanWrite(updated).Should().BeFalse();

            var list = await ReadAsync(writer, HttpMethod.Get, "/v1/chats");
            var items = list.GetProperty("resources").EnumerateArray().ToArray();
            items.Should().Contain(c => c.GetProperty("id").GetGuid() == chatId);
            items.Should().AllSatisfy(c =>
                (c.TryGetProperty("viewer", out var viewer) && viewer.ValueKind != JsonValueKind.Null)
                .Should().BeFalse());
        }
        finally
        {
            await DropAsync(chats, writer, addressee, third);
        }
    }

    // ═══ SEEDING ═══

    private static object Text => new { text = "Сообщение, которое предлагает страница" };

    private static UserBlacklistSettings SettingsOf(Block block) => block == Block.WithoutTheSwitch
        ? UserBlacklistSettings.HideComments | UserBlacklistSettings.HideMessages
        : UserBlacklistSettings.Default;

    private async Task<GeneralUser> SeedUserAsync(
        string prefix, UserBlacklistSettings settings = UserBlacklistSettings.Default)
    {
        var userId = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"{prefix}{userId:N}"[..20],
            Email = $"{userId:N}@canmessageflag.example",
            PasswordHash = "hash",
            Salt = "salt",
            BlacklistSettings = settings
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new GeneralUser { UserId = userId, Username = user.Username, Role = UserRole.RegularUser };
    }

    /// <summary>The owner puts the user on his personal blacklist.</summary>
    private async Task BlockAsync(GeneralUser owner, GeneralUser blocked)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        db.UserBlacklists.Add(new DbUserBlacklist
        {
            EntryId = Guid.NewGuid(),
            OwnerId = owner.UserId,
            BlockedUserId = blocked.UserId,
            CreatedUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task<string> PublicIdAsync(Guid chatId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        return (await db.Chats.Where(c => c.ChatId == chatId).Select(c => c.PublicId).SingleAsync())!;
    }

    private async Task<Guid?> GlobalLastMessageAsync()
    {
        await using var db = DatabaseFixture.CreateDbContext();
        return await db.Chats.Where(c => c.ChatId == WellKnownChats.GlobalChatId)
            .Select(c => c.LastMessageId).SingleAsync();
    }

    /// <summary>
    /// The lines the case wrote in the global chat, with the chat's last message
    /// put back to what it was before them.
    /// </summary>
    private async Task DropGlobalMessagesAsync(GeneralUser writer, Guid? lastBefore)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.Chats.Where(c => c.ChatId == WellKnownChats.GlobalChatId &&
                                  db.Set<DbMessage>().IgnoreQueryFilters()
                                      .Any(m => m.MessageId == c.LastMessageId && m.UserId == writer.UserId))
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.LastMessageId, lastBefore));
        await db.Set<DbMessage>().IgnoreQueryFilters()
            .Where(m => m.ChatId == WellKnownChats.GlobalChatId && m.UserId == writer.UserId)
            .ExecuteDeleteAsync();
    }

    /// <summary>
    /// The conversations of the case, their messages, markers and links, and the
    /// blacklist entries of its people. The people stay.
    /// </summary>
    private async Task DropAsync(IReadOnlyCollection<Guid> chats, params GeneralUser[] people)
    {
        var ids = people.Select(p => p.UserId).ToArray();
        await using var db = DatabaseFixture.CreateDbContext();
        await db.Chats.Where(c => chats.Contains(c.ChatId))
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.LastMessageId, (Guid?)null));
        await db.Set<DbMessage>().IgnoreQueryFilters().Where(m => chats.Contains(m.ChatId)).ExecuteDeleteAsync();
        await db.UnreadCounters.Where(c => chats.Contains(c.EntityId)).ExecuteDeleteAsync();
        await db.UserChatLinks.IgnoreQueryFilters().Where(l => chats.Contains(l.ChatId)).ExecuteDeleteAsync();
        await db.Chats.Where(c => chats.Contains(c.ChatId)).ExecuteDeleteAsync();
        await db.UserBlacklists.Where(b => ids.Contains(b.OwnerId) || ids.Contains(b.BlockedUserId))
            .ExecuteDeleteAsync();
    }

    // ═══ REQUESTS ═══

    /// <summary>A request as the viewer, under an ordinary ban when asked; a guest sends none.</summary>
    private Task<HttpResponseMessage> SendAsync(
        GeneralUser? user, HttpMethod method, string url, object? body = null, bool banned = false)
    {
        var request = user is null
            ? new HttpRequestMessage(method, url)
            : CreateAuthenticatedRequest(method, url, user);
        if (user is not null && banned)
        {
            request.Headers.Add(TestAuthenticationMiddleware.TestUserAccessPolicyHeader,
                AccessPolicy.DemocraticBan.ToString());
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return Client.SendAsync(request);
    }

    /// <summary>A successful answer, read whole. The chat routes answer it bare.</summary>
    private async Task<JsonElement> ReadAsync(
        GeneralUser? user, HttpMethod method, string url, object? body = null, bool banned = false)
    {
        var response = await SendAsync(user, method, url, body, banned);
        var text = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"{method} {url} answered {response.StatusCode}: {text}");
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    /// <summary>The profile the page reads, out of its envelope.</summary>
    private async Task<JsonElement> ProfileAsync(GeneralUser? user, string username, bool banned = false) =>
        (await ReadAsync(user, HttpMethod.Get, $"/v1/users/{Uri.EscapeDataString(username)}/profile",
            banned: banned)).GetProperty("resource");

    private static bool CanWrite(JsonElement chat) =>
        chat.GetProperty("viewer").GetProperty("canWrite").GetBoolean();
}
