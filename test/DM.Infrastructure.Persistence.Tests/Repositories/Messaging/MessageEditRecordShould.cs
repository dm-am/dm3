using DM.Domain.Core.Chats;
using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.Messages;
using DM.Infrastructure.Persistence.Repositories.Messaging;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Messaging;

/// <summary>
/// Editing a message leaves a trace.
/// </summary>
/// <remarks>
/// A warning on a global chat message reads from this history whether the author rewrote it. Re-sending the
/// held text, or a write naming no editor, leaves the history alone.
/// </remarks>
public class MessageEditRecordShould : UnitTestBase
{
    private static readonly DateTimeOffset Created = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Edited = new(2026, 5, 2, 11, 0, 0, TimeSpan.Zero);

    private static readonly Guid AuthorId = Guid.Parse("7c2e1d33-0000-4000-8000-000000000001");
    private static readonly Guid MessageId = Guid.Parse("7c2e1d33-0000-4000-8000-000000000002");
    private static readonly Guid EditId = Guid.Parse("7c2e1d33-0000-4000-8000-000000000003");

    private static DmDbContext Seeded()
    {
        var context = InMemoryStore.Open();
        context.Users.Add(InMemoryStore.User(AuthorId, "author"));
        context.Chats.Add(new DbChat { ChatId = WellKnownChats.GlobalChatId, Type = ChatType.Global });
        context.Messages.Add(new DbMessage
        {
            MessageId = MessageId,
            ChatId = WellKnownChats.GlobalChatId,
            UserId = AuthorId,
            Text = "Исходный текст",
            CreatedUtc = Created
        });
        context.SaveChanges();
        return context;
    }

    private static MessageRepository Repository(DmDbContext context)
    {
        var clock = Mock<IDateTimeProvider>();
        clock.Now.Returns(Edited);
        var guids = Mock<IGuidFactory>();
        guids.Create().Returns(EditId);
        return new MessageRepository(context, Mock<ICursorService>(), clock, guids);
    }

    [Fact]
    public async Task RecordWhoRewroteTheMessageAndWhen()
    {
        using var context = Seeded();

        await Repository(context).Update(new UpdateMessageEntity
        {
            MessageId = MessageId,
            Text = "Переписанный текст",
            EditorUserId = AuthorId
        });

        var edits = await context.MessageEdits.Where(e => e.MessageId == MessageId).ToListAsync();

        edits.Should().ContainSingle();
        edits[0].EditorUserId.Should().Be(AuthorId);
        edits[0].ModifiedUtc.Should().Be(Edited);
    }

    [Fact]
    public async Task RecordNothingWhenTheTextDoesNotChange()
    {
        using var context = Seeded();

        await Repository(context).Update(new UpdateMessageEntity
        {
            MessageId = MessageId,
            Text = "Исходный текст",
            EditorUserId = AuthorId
        });

        (await context.MessageEdits.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RecordNothingWhenTheCallerDoesNotSayWhoIsEditing()
    {
        using var context = Seeded();

        await Repository(context).Update(new UpdateMessageEntity
        {
            MessageId = MessageId,
            Text = "Переписанный текст"
        });

        // EditorUserId has a foreign key to Users, so writing the trace anyway
        // would put Guid.Empty in that column and take the edit down with it.
        (await context.MessageEdits.CountAsync()).Should().Be(0);
    }
}
