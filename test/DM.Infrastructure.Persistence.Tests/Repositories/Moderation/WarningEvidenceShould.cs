using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Enums;
using DM.Domain.Moderation.Features.Warnings;
using DM.Infrastructure.Persistence.Repositories.Moderation;
using DM.Infrastructure.Persistence.Shared.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbCommentEdit = DM.Infrastructure.Persistence.Entities.Shared.CommentEdit;
using DbBoard = DM.Infrastructure.Persistence.Entities.Forum.Board;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Moderation;

/// <summary>
/// A warning keeps the text it was issued for, and says so when that text has
/// since changed.
/// </summary>
/// <remarks>
/// The warning named the offending object by identifier and nothing else. The
/// author of that object may edit it, and no edit history on the site keeps the
/// previous text — so the person who was warned could erase the evidence for
/// their own warning by rewriting one sentence, and the moderation page would
/// show a link to a text that never contained the violation. Deleting the object
/// removed it entirely.
///
/// The snapshot is a column and is written once; the address and the "edited
/// since" mark are not, because both change without the warning being touched.
/// Asserted together on purpose: a snapshot that nothing compares against is a
/// copy nobody can date, and a mark with no snapshot beside it says a text
/// changed without saying from what.
/// </remarks>
public class WarningEvidenceShould
{
    private static readonly DateTimeOffset Written = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Warned = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Rewritten = new(2026, 5, 1, 14, 0, 0, TimeSpan.Zero);

    private static readonly Guid AuthorId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000001");
    private static readonly Guid ModeratorId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000002");
    private static readonly Guid BoardId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000011");
    private static readonly Guid TopicId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000012");
    private static readonly Guid CommentId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000013");
    private static readonly Guid MessageId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000014");
    private static readonly Guid RoomChatId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000015");
    private static readonly Guid RoomMessageId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000016");
    private static readonly Guid DirectChatId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000017");
    private static readonly Guid DirectMessageId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000018");
    private static readonly Guid WarningId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000021");

    private const string Offending = "Исходный текст, за который вынесено предупреждение";

    private readonly string _databaseName = Guid.NewGuid().ToString();

    private DmDbContext Context() => new(new DbContextOptionsBuilder<DmDbContext>()
        .UseInMemoryDatabase(_databaseName)
        .Options);

    private static IMapper Mapper() => new MapperConfiguration(cfg =>
    {
        cfg.AddProfile<GeneralUserMappingProfile>();
        cfg.AddProfile<ModerationMappingProfile>();
    }).CreateMapper();

    private static DbUser NewUser(Guid id, string username) => new()
    {
        UserId = id,
        Username = username,
        Email = $"{username}@test.local",
        Salt = "",
        PasswordHash = ""
    };

    /// <summary>
    /// A forum topic with one comment under it, and one message in each of the
    /// three chats that matter here: the global one, a game room's, and a private
    /// conversation.
    /// </summary>
    private DmDbContext Seeded()
    {
        var context = Context();
        context.Users.AddRange(NewUser(AuthorId, "author"), NewUser(ModeratorId, "moderator"));
        context.Boards.Add(new DbBoard { BoardId = BoardId, Title = "Флуд", Alias = "flood" });
        context.Topics.Add(new DbTopic
        {
            TopicId = TopicId,
            BoardId = BoardId,
            TopicNumber = 12,
            AuthorId = AuthorId,
            Title = "Тема",
            Text = Offending,
            CreatedUtc = Written
        });
        context.Comments.Add(new DbComment
        {
            CommentId = CommentId,
            EntityId = TopicId,
            AuthorId = AuthorId,
            Text = Offending,
            CreatedUtc = Written
        });
        context.Chats.AddRange(
            new DbChat { ChatId = DbChat.GlobalChatId, Type = ChatType.Global },
            new DbChat { ChatId = RoomChatId, Type = ChatType.GameRoom, RoomId = Guid.NewGuid() },
            new DbChat { ChatId = DirectChatId, Type = ChatType.Direct });
        context.Messages.AddRange(
            new DbMessage
            {
                MessageId = MessageId,
                ChatId = DbChat.GlobalChatId,
                UserId = AuthorId,
                Text = Offending,
                CreatedUtc = Written
            },
            new DbMessage
            {
                MessageId = RoomMessageId,
                ChatId = RoomChatId,
                UserId = AuthorId,
                Text = "Реплика в чате игровой комнаты",
                CreatedUtc = Written
            },
            new DbMessage
            {
                MessageId = DirectMessageId,
                ChatId = DirectChatId,
                UserId = AuthorId,
                Text = "Личное сообщение",
                CreatedUtc = Written
            });
        context.SaveChanges();
        return context;
    }

    private static WarningEntityResolver Resolver(DmDbContext context) => new(context);

    private static WarningEntityRequest Request(Guid entityId, WarningEntityType entityType) => new()
    {
        WarningId = WarningId,
        EntityId = entityId,
        EntityType = entityType,
        IssuedUtc = Warned
    };

    private static async Task<WarningEntityState> Resolve(
        DmDbContext context, Guid entityId, WarningEntityType entityType)
    {
        var states = await Resolver(context).ResolveStates([Request(entityId, entityType)]);
        return states[WarningId];
    }

    [Theory]
    [InlineData(WarningEntityType.Comment)]
    [InlineData(WarningEntityType.Topic)]
    [InlineData(WarningEntityType.Message)]
    public async Task TakeTheTextTheModeratorWasReading(WarningEntityType entityType)
    {
        using var context = Seeded();
        var entityId = entityType switch
        {
            WarningEntityType.Comment => CommentId,
            WarningEntityType.Topic => TopicId,
            _ => MessageId
        };

        var snapshot = await Resolver(context).CaptureSnapshot(entityType, entityId);

        snapshot.Should().Be(Offending);
    }

    /// <summary>
    /// Game content is not moderated: no warn button leads to a post, and nothing
    /// here resolves one. The line is drawn in code rather than left to the
    /// absence of a caller.
    /// </summary>
    [Fact]
    public async Task LeaveGameContentAlone()
    {
        using var context = Seeded();

        var snapshot = await Resolver(context).CaptureSnapshot(WarningEntityType.Post, CommentId);

        snapshot.Should().BeNull();
    }

    /// <summary>
    /// One table holds four kinds of conversation, and only the global chat is
    /// moderated. A game room's chat and a private one answer with nothing, so
    /// that no warning ever carries a copy of either.
    /// </summary>
    /// <remarks>
    /// The branch used to read Messages by identifier alone. A moderator holding
    /// a message id — theirs by being in the conversation, or guessed — could
    /// have the body of a personal letter or of a game room copied verbatim onto
    /// a warning, where it stays for as long as the warning does. The address is
    /// already gated on the same chat; the snapshot was not.
    /// </remarks>
    [Fact]
    public async Task TakeNothingFromAChatOutsideModeration()
    {
        using var context = Seeded();
        var resolver = Resolver(context);

        var fromRoom = await resolver.CaptureSnapshot(WarningEntityType.Message, RoomMessageId);
        var fromDirect = await resolver.CaptureSnapshot(WarningEntityType.Message, DirectMessageId);

        fromRoom.Should().BeNull("a game room chat is not moderated");
        fromDirect.Should().BeNull("a private conversation is not moderation's to copy");
    }

    /// <summary>
    /// And neither gets an address, as before: nothing about them is answered.
    /// </summary>
    [Fact]
    public async Task AddressNoChatOutsideModeration()
    {
        using var context = Seeded();

        var fromRoom = await Resolve(context, RoomMessageId, WarningEntityType.Message);
        var fromDirect = await Resolve(context, DirectMessageId, WarningEntityType.Message);

        fromRoom.Url.Should().BeNull();
        fromDirect.Url.Should().BeNull();
    }

    /// <summary>
    /// The whole point: the author rewrites the comment, the stored snapshot does
    /// not follow, and the moderator is told the two have parted.
    /// </summary>
    [Fact]
    public async Task KeepTheSnapshotAndRaiseTheMarkWhenTheTextIsRewrittenAfterwards()
    {
        using var context = Seeded();
        var repository = new WarningRepository(context, Mapper());

        var snapshot = await Resolver(context).CaptureSnapshot(WarningEntityType.Comment, CommentId);
        await repository.Create(new CreateWarningEntity
        {
            WarningId = WarningId,
            TargetUserId = AuthorId,
            AuthorId = ModeratorId,
            EntityId = CommentId,
            EntityType = WarningEntityType.Comment,
            Points = 2,
            Text = "Оскорбление",
            EntitySnapshot = snapshot,
            CreatedUtc = Warned
        });

        var comment = await context.Comments.SingleAsync(c => c.CommentId == CommentId);
        comment.Text = "Безобидный текст";
        context.CommentEdits.Add(new DbCommentEdit
        {
            CommentEditId = Guid.NewGuid(),
            CommentId = CommentId,
            EditorUserId = AuthorId,
            EditedUtc = Rewritten
        });
        await context.SaveChangesAsync();

        var stored = await repository.Get(WarningId);
        var state = await Resolve(context, CommentId, WarningEntityType.Comment);

        stored!.EntitySnapshot.Should().Be(Offending);
        state.EditedAfterWarning.Should().BeTrue();
    }

    /// <summary>
    /// An edit that happened before the warning is not what the mark is about:
    /// the moderator was already reading the edited text when they pressed
    /// "warn", so the snapshot and the object still agree.
    /// </summary>
    [Fact]
    public async Task NotRaiseTheMarkForAnEditThatPrecededTheWarning()
    {
        using var context = Seeded();
        context.CommentEdits.Add(new DbCommentEdit
        {
            CommentEditId = Guid.NewGuid(),
            CommentId = CommentId,
            EditorUserId = AuthorId,
            EditedUtc = Written
        });
        await context.SaveChangesAsync();

        var state = await Resolve(context, CommentId, WarningEntityType.Comment);

        state.EditedAfterWarning.Should().BeFalse();
    }

    /// <summary>
    /// Deleting the object takes the link with it and nothing else: the warning
    /// still reads, and the snapshot is all that is left of what was written.
    /// </summary>
    [Fact]
    public async Task SurviveTheDeletionOfTheObject()
    {
        using var context = Seeded();
        var repository = new WarningRepository(context, Mapper());

        var snapshot = await Resolver(context).CaptureSnapshot(WarningEntityType.Comment, CommentId);
        await repository.Create(new CreateWarningEntity
        {
            WarningId = WarningId,
            TargetUserId = AuthorId,
            AuthorId = ModeratorId,
            EntityId = CommentId,
            EntityType = WarningEntityType.Comment,
            Points = 2,
            Text = "Оскорбление",
            EntitySnapshot = snapshot,
            CreatedUtc = Warned
        });

        var comment = await context.Comments.SingleAsync(c => c.CommentId == CommentId);
        comment.IsRemoved = true;
        await context.SaveChangesAsync();

        var stored = await repository.Get(WarningId);
        var state = await Resolve(context, CommentId, WarningEntityType.Comment);

        stored!.EntitySnapshot.Should().Be(Offending);
        state.Url.Should().BeNull();
    }

    /// <summary>
    /// The addresses are the ones the client router serves, so the link the
    /// moderator follows lands on the object rather than on a 404.
    /// </summary>
    [Fact]
    public async Task AddressTheObjectTheWayTheClientRoutesIt()
    {
        using var context = Seeded();

        var states = await Resolver(context).ResolveStates(
        [
            new WarningEntityRequest
            {
                WarningId = TopicId, EntityId = TopicId,
                EntityType = WarningEntityType.Topic, IssuedUtc = Warned
            },
            new WarningEntityRequest
            {
                WarningId = CommentId, EntityId = CommentId,
                EntityType = WarningEntityType.Comment, IssuedUtc = Warned
            },
            new WarningEntityRequest
            {
                WarningId = MessageId, EntityId = MessageId,
                EntityType = WarningEntityType.Message, IssuedUtc = Warned
            }
        ]);

        states[TopicId].Url.Should().Be("/forum/flood/12");
        states[CommentId].Url.Should().Be($"/forum/flood/12#comment-{CommentId}");
        states[MessageId].Url.Should().Be($"/global-chat#msg-{MessageId}");
    }

    /// <summary>
    /// A warning naming no object asks nothing and answers nothing.
    /// </summary>
    [Fact]
    public async Task AnswerNothingForAWarningThatNamesNoObject()
    {
        using var context = Seeded();

        var states = await Resolver(context).ResolveStates(new List<WarningEntityRequest>());

        states.Should().BeEmpty();
    }
}
