using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.Messages;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// A chat names its newest message, whatever order two writers commit in.
/// </summary>
/// <remarks>
/// LastMessageId orders the conversations and gives the preview, and CreatedUtc is stamped before the write, so the
/// last commit is not always the newest. Against PostgreSQL: a row lock and a conditional set-based statement.
/// </remarks>
public class ChatLastMessagePointerShould : IntegrationTestBase
{
    private static readonly DateTimeOffset Written = new(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// How long the queued call is watched before it is called blocked. Long
    /// enough that a call which does not wait at all has finished.
    /// </summary>
    private static readonly TimeSpan Probe = TimeSpan.FromSeconds(2);

    /// <summary>The bound on the whole wait, so a lock nobody releases fails.</summary>
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(30);

    public ChatLastMessagePointerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task PointAtTheNewerOfTwoMessagesWrittenToOneChatAtOnce()
    {
        var chatId = await AddChat();

        var messageIds = await Task.WhenAll(
            CreateMessage(chatId, Written),
            CreateMessage(chatId, Written.AddMinutes(1)));

        var chat = await Reread(chatId);
        chat.LastMessageId.Should().Be(messageIds[1],
            "the pointer is the conversation's last line, and which of two simultaneous " +
            "sends commits last says nothing about which was written later");

        (await CountMessages(chatId)).Should().Be(2, "both messages were sent");
    }

    /// <summary>
    /// The pointer does not walk backwards when an older message lands last.
    /// </summary>
    /// <remarks>In order, not in parallel: which concurrent send commits last cannot be pinned.</remarks>
    [Fact]
    public async Task LeaveThePointerOnTheNewerMessageWhenAnOlderOneArrivesAfterIt()
    {
        var chatId = await AddChat();

        var newer = await CreateMessage(chatId, Written.AddMinutes(10));
        await CreateMessage(chatId, Written);

        var chat = await Reread(chatId);
        chat.LastMessageId.Should().Be(newer,
            "the message that arrived late was written earlier than the one it would " +
            "have replaced, and the list of conversations orders by this pointer");

        (await CountMessages(chatId)).Should().Be(2, "both messages were sent");
    }

    /// <summary>
    /// A send waits for the send in flight beside it and does not take the
    /// conversation's last line backwards.
    /// </summary>
    /// <remarks>
    /// The newer message is held open by hand in the repository's shape and the older one sent by the real call,
    /// which must read the chat after the wait.
    /// </remarks>
    [Fact]
    public async Task WaitForTheSendInFlightAndLeaveItsNewerMessageOnThePointer()
    {
        var chatId = await AddChat();
        var newerInFlight = Guid.NewGuid();

        await using var sending = DatabaseFixture.CreateDbContext();
        await using var send = await sending.Database.BeginTransactionAsync();
        await sending.Database.ExecuteSqlRawAsync(
            """SELECT "ChatId" FROM "Chats" WHERE "ChatId" = {0} FOR UPDATE""", chatId);
        await InsertMessage(sending, chatId, newerInFlight, Written.AddMinutes(10));
        await sending.Database.ExecuteSqlRawAsync(
            """UPDATE "Chats" SET "LastMessageId" = {0} WHERE "ChatId" = {1}""", newerInFlight, chatId);

        var older = Guid.NewGuid();
        var queued = Task.Run(() => CreateMessage(chatId, Written, older));
        var decidedWhileTheOtherWasOpen = await Task.WhenAny(queued, Task.Delay(Probe)) == queued;

        await send.CommitAsync();
        await queued.WaitAsync(Bound);

        decidedWhileTheOtherWasOpen.Should().BeFalse(
            "the pointer is decided against the chat row, and the send in flight holds it");

        var chat = await Reread(chatId);
        chat.LastMessageId.Should().Be(newerInFlight,
            "the message that arrived last was written earlier than the one already on the " +
            "pointer, and the list of conversations orders by this pointer");
        (await CountMessages(chatId)).Should().Be(2, "both messages were sent");
    }

    /// <summary>
    /// A deletion of the last line decides the pointer after the send in flight
    /// beside it.
    /// </summary>
    /// <remarks>The successor is read from the chat, and a read cannot see an uncommitted message.</remarks>
    [Fact]
    public async Task LeaveThePointerOnTheMessageSentWhileTheLastLineWasBeingDeleted()
    {
        var chatId = await AddChat();
        await CreateMessage(chatId, Written);
        var lastLine = await CreateMessage(chatId, Written.AddMinutes(5));
        var sentInFlight = Guid.NewGuid();

        await using var sending = DatabaseFixture.CreateDbContext();
        await using var send = await sending.Database.BeginTransactionAsync();
        await sending.Database.ExecuteSqlRawAsync(
            """SELECT "ChatId" FROM "Chats" WHERE "ChatId" = {0} FOR UPDATE""", chatId);
        await InsertMessage(sending, chatId, sentInFlight, Written.AddMinutes(10));
        await sending.Database.ExecuteSqlRawAsync(
            """UPDATE "Chats" SET "LastMessageId" = {0} WHERE "ChatId" = {1}""", sentInFlight, chatId);

        var deletion = Task.Run(() => DeleteMessage(lastLine));
        var decidedWhileTheSendWasOpen = await Task.WhenAny(deletion, Task.Delay(Probe)) == deletion;

        await send.CommitAsync();
        await deletion.WaitAsync(Bound);

        decidedWhileTheSendWasOpen.Should().BeFalse(
            "the deletion takes the chat row before it reads the pointer it may move, and " +
            "the send in flight holds that row");

        var chat = await Reread(chatId);
        chat.LastMessageId.Should().Be(sentInFlight,
            "the line being removed was no longer the chat's last one by the time the " +
            "deletion looked, so there was no pointer for it to move");
    }

    /// <summary>
    /// The pointer names the message this call wrote.
    /// </summary>
    /// <remarks>The pointer statement compares (CreatedUtc, MessageId), which must come from one message.</remarks>
    [Fact]
    public async Task PointAtTheMessageItWroteAndNotAtTheOneNamedBesideIt()
    {
        var chatId = await AddChat();
        var messageId = Guid.NewGuid();

        using (var scope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
            await repository.Create(
                new CreateMessageEntity
                {
                    MessageId = messageId,
                    ChatId = chatId,
                    UserId = TestConstants.TestUserId,
                    Text = "Сообщение",
                    CreatedUtc = Written,
                    IsRemoved = false,
                },
                new UpdateChatLastMessageEntity
                {
                    ChatId = chatId,
                    LastMessageId = Guid.NewGuid(),
                });
        }

        var chat = await Reread(chatId);
        chat.LastMessageId.Should().Be(messageId,
            "the message written is the one the chat's last line can name, and no other " +
            "identifier reaches the comparison the pointer moves under");
    }

    private static Task InsertMessage(
        DM.Infrastructure.Persistence.DmDbContext dbContext,
        Guid chatId,
        Guid messageId,
        DateTimeOffset createdUtc) =>
        dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Messages" ("MessageId", "ChatId", "UserId", "Text", "CreatedUtc", "IsRemoved")
            VALUES ({0}, {1}, {2}, 'Сообщение рядом', {3}, false)
            """,
            messageId, chatId, TestConstants.TestUserId, createdUtc);

    private async Task DeleteMessage(Guid messageId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
        await repository.Delete(messageId, TestConstants.TestUserId);
    }

    // One scope per concurrent call: a DbContext is scoped and must not be shared
    // between parallel callers.
    private Task<Guid> CreateMessage(Guid chatId, DateTimeOffset createdUtc) =>
        CreateMessage(chatId, createdUtc, Guid.NewGuid());

    private async Task<Guid> CreateMessage(Guid chatId, DateTimeOffset createdUtc, Guid messageId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMessageRepository>();
        await repository.Create(
            new CreateMessageEntity
            {
                MessageId = messageId,
                ChatId = chatId,
                UserId = TestConstants.TestUserId,
                Text = "Сообщение",
                CreatedUtc = createdUtc,
                IsRemoved = false,
            },
            new UpdateChatLastMessageEntity
            {
                ChatId = chatId,
                LastMessageId = messageId,
            });
        return messageId;
    }

    // Participant links are left out: the pointer is a column of the chat row and
    // nothing on the write path reads who is in the conversation.
    private async Task<Guid> AddChat()
    {
        var chatId = Guid.NewGuid();
        await using var dbContext = DatabaseFixture.CreateDbContext();
        dbContext.Chats.Add(new DbChat
        {
            ChatId = chatId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Type = ChatType.Direct,
            Title = string.Empty,
        });
        await dbContext.SaveChangesAsync();
        return chatId;
    }

    private async Task<DbChat> Reread(Guid chatId)
    {
        await using var dbContext = DatabaseFixture.CreateDbContext();
        return await dbContext.Chats.AsNoTracking().SingleAsync(c => c.ChatId == chatId);
    }

    private async Task<int> CountMessages(Guid chatId)
    {
        await using var dbContext = DatabaseFixture.CreateDbContext();
        return await dbContext.Messages.CountAsync(m => m.ChatId == chatId);
    }
}
