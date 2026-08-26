using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.Messages;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Repositories.Messaging;
using DM.Infrastructure.Persistence.Shared.Users;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Messaging;

/// <summary>
/// Editing a message leaves a trace.
/// </summary>
/// <remarks>
/// The message row keeps no modification stamp: the update path said tracking
/// was "handled via Edit history" and then saved the new text alone, so
/// MessageEdits stayed empty for every message ever edited — the same shape of
/// hole the four comment repositories had.
///
/// It is load-bearing beyond the "edited" mark. A moderation warning on a global
/// chat message shows the text as it stood when the warning was issued, and says
/// whether the author has rewritten it since; that answer is read from this
/// table, so an unwritten history reports every rewritten message as untouched.
///
/// Re-sending the text the message already holds is not an edit, and neither is
/// a write that does not say who is making it: both leave the history alone.
/// </remarks>
public class MessageEditRecordShould : UnitTestBase
{
    private static readonly DateTimeOffset Created = new(2026, 5, 2, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Edited = new(2026, 5, 2, 11, 0, 0, TimeSpan.Zero);

    private static readonly Guid AuthorId = Guid.Parse("7c2e1d33-0000-4000-8000-000000000001");
    private static readonly Guid MessageId = Guid.Parse("7c2e1d33-0000-4000-8000-000000000002");
    private static readonly Guid EditId = Guid.Parse("7c2e1d33-0000-4000-8000-000000000003");

    private readonly string _databaseName = Guid.NewGuid().ToString();

    private DmDbContext Context() => new(new DbContextOptionsBuilder<DmDbContext>()
        .UseInMemoryDatabase(_databaseName)
        .Options);

    private DmDbContext Seeded()
    {
        var context = Context();
        context.Users.Add(new DbUser
        {
            UserId = AuthorId,
            Username = "author",
            Email = "author@test.local",
            Salt = "",
            PasswordHash = ""
        });
        context.Chats.Add(new DbChat { ChatId = DbChat.GlobalChatId, Type = ChatType.Global });
        context.Messages.Add(new DbMessage
        {
            MessageId = MessageId,
            ChatId = DbChat.GlobalChatId,
            UserId = AuthorId,
            Text = "Исходный текст",
            CreatedUtc = Created
        });
        context.SaveChanges();
        return context;
    }

    private MessageRepository Repository(DmDbContext context)
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
