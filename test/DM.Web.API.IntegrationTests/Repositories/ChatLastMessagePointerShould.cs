using System;
using System.Threading.Tasks;
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
/// LastMessageId is what the list of conversations orders by and what the preview
/// under each of them shows. It used to be assigned unconditionally by every send:
/// CreatedUtc is stamped before the write reaches the chat row, so the message that
/// commits last is not necessarily the newest one, and a send that waited on a lock,
/// a retry or a slow round trip pointed the conversation back at an older line.
///
/// Run against PostgreSQL because the fix is a row lock plus a conditional set-based
/// statement: neither has an in-memory implementation, and the defect is not
/// observable at all without two writers running against one chat.
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
    /// <remarks>
    /// Written in order rather than in parallel: which of two concurrent sends commits
    /// last is exactly what a test cannot pin down, and the defect is about the one that
    /// does.
    /// </remarks>
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
    /// The same promise as the two above, pinned on an ordering rather than left
    /// to which of two parallel calls the scheduler starts first: the newer
    /// message is held open by hand, in the shape the repository sends one in (the
    /// chat row taken, the message written, the pointer moved), and the older
    /// message is sent by the real call. What that call must not do is move the
    /// pointer to itself, and it can only know not to by reading the chat after
    /// the wait.
    ///
    /// Only a live database can be asked whether one write waits for another,
    /// which is why this is here and not in the domain tests.
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
    /// <remarks>
    /// The successor of the message being removed is read from the chat, and a read
    /// cannot see the message another transaction has written but not committed: the
    /// deletion picked the newest line it could see, which was an older one, and wrote
    /// it over the pointer the send had just moved. The conversation then showed a
    /// preview older than its own last message, and nothing recomputes the pointer
    /// afterwards.
    /// </remarks>
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
    /// <remarks>
    /// The chat update handed in beside the message carries an identifier of its
    /// own, and the statement that moves the pointer compares the chat's current
    /// line against the pair (CreatedUtc, MessageId). Built from one message's
    /// timestamp and another's identifier, that pair belongs to no message at all:
    /// the comparison is made against a row nobody wrote, and whatever it answers
    /// the pointer ends up naming a line whose timestamp was never looked at. The
    /// two identifiers agree everywhere they are handed in from today, which is
    /// exactly why nothing would report it if one day they stopped.
    /// </remarks>
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
