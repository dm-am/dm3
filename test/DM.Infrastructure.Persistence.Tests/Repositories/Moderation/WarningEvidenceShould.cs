using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Moderation.Features.Warnings;
using DM.Infrastructure.Persistence.Repositories.Moderation;
using DM.Infrastructure.Persistence.Shared.Users;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;
using DbBoard = DM.Infrastructure.Persistence.Entities.Forum.Board;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Moderation;

/// <summary>
/// A warning keeps the text it was issued for, whatever happens to that text
/// afterwards.
/// </summary>
/// <remarks>
/// The warning named the offending object by identifier and nothing else. The
/// author of that object may edit it, and no edit history on the site keeps the
/// previous text — so the person who was warned could erase the evidence for
/// their own warning by rewriting one sentence, and the moderation page would
/// show a link to a text that never contained the violation. Deleting the object
/// removed it entirely.
///
/// The snapshot is a column and is written once; the address is not, because
/// it changes without the warning being touched. Whether the object was edited
/// after the warning is not read at all: the log shows the snapshot, the text
/// the warning was given for, and a later edit is no part of it.
/// </remarks>
public class WarningEvidenceShould
{
    private static readonly DateTimeOffset Written = new(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Warned = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

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
    private static readonly Guid GameId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000031");
    private static readonly Guid GameCommentId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000032");
    private static readonly Guid BlogId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000033");
    private static readonly Guid BlogCommentId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000034");
    private static readonly Guid PublicationId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000035");
    private static readonly Guid PublicationCommentId = Guid.Parse("6b1f0c22-0000-4000-8000-000000000036");

    private const string Offending = "Исходный текст, за который вынесено предупреждение";

    private readonly string _databaseName = Guid.NewGuid().ToString();

    private DmDbContext Context() => new(new DbContextOptionsBuilder<DmDbContext>()
        .UseInMemoryDatabase(_databaseName)
        .Options);

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
        context.Boards.Add(new DbBoard
        {
            BoardId = BoardId,
            Title = "Флуд",
            Alias = "flood",
            ViewPolicy = BoardAccessPolicy.Guest | BoardAccessPolicy.RegularUser
        });
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
        EntityType = entityType
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
    /// The whole point: the author rewrites the comment, and the stored snapshot
    /// does not follow.
    /// </summary>
    [Fact]
    public async Task KeepTheSnapshotWhenTheTextIsRewrittenAfterwards()
    {
        using var context = Seeded();
        var repository = new WarningRepository(context);

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
        await context.SaveChangesAsync();

        var stored = await repository.Get(WarningId);

        stored!.EntitySnapshot.Should().Be(Offending);
    }

    /// <summary>
    /// Deleting the object takes the link with it and nothing else: the warning
    /// still reads, and the snapshot is all that is left of what was written.
    /// </summary>
    [Fact]
    public async Task SurviveTheDeletionOfTheObject()
    {
        using var context = Seeded();
        var repository = new WarningRepository(context);

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
                EntityType = WarningEntityType.Topic
            },
            new WarningEntityRequest
            {
                WarningId = CommentId, EntityId = CommentId,
                EntityType = WarningEntityType.Comment
            },
            new WarningEntityRequest
            {
                WarningId = MessageId, EntityId = MessageId,
                EntityType = WarningEntityType.Message
            }
        ]);

        states[TopicId].Url.Should().Be("/forum/flood/12");
        states[CommentId].Url.Should().Be($"/forum/flood/12#comment-{CommentId}");
        states[MessageId].Url.Should().Be($"/global-chat#msg-{MessageId}");
    }

    /// <summary>
    /// The log names where the text stood and when it was written, as DM2's
    /// caption did: the topic by its title, the global chat by its kind.
    /// </summary>
    [Fact]
    public async Task NameThePlaceAndTheDateOfTheText()
    {
        using var context = Seeded();

        var comment = await Resolve(context, CommentId, WarningEntityType.Comment);
        var topic = await Resolve(context, TopicId, WarningEntityType.Topic);
        var message = await Resolve(context, MessageId, WarningEntityType.Message);

        comment.PlaceKind.Should().Be(WarningPlaceKind.Topic);
        comment.PlaceTitle.Should().Be("Тема");
        comment.CreatedUtc.Should().Be(Written);
        comment.LogClosure.Should().BeNull("a guest reads this board");
        topic.PlaceKind.Should().Be(WarningPlaceKind.Topic);
        topic.PlaceTitle.Should().Be("Тема");
        message.PlaceKind.Should().Be(WarningPlaceKind.GlobalChat);
        message.PlaceTitle.Should().BeNull("the global chat is named by its kind");
        message.LogClosure.Should().BeNull();
    }

    /// <summary>
    /// A board closed to guests keeps its texts out of the open log.
    /// </summary>
    [Fact]
    public async Task CloseTheLogOverABoardAGuestCannotRead()
    {
        using var context = Seeded();
        var board = await context.Boards.SingleAsync(b => b.BoardId == BoardId);
        board.ViewPolicy = BoardAccessPolicy.RegularUser;
        await context.SaveChangesAsync();

        (await Resolve(context, CommentId, WarningEntityType.Comment)).LogClosure
            .Should().Be(WarningLogClosure.BoardClosed);
        (await Resolve(context, TopicId, WarningEntityType.Topic)).LogClosure
            .Should().Be(WarningLogClosure.BoardClosed);
    }

    /// <summary>
    /// A comment under a deleted topic is closed with it, and loses the address.
    /// </summary>
    [Fact]
    public async Task CloseTheLogOverADeletedTopic()
    {
        using var context = Seeded();
        var topic = await context.Topics.SingleAsync(t => t.TopicId == TopicId);
        topic.IsRemoved = true;
        topic.DeletedByUserId = ModeratorId;
        await context.SaveChangesAsync();

        var state = await Resolve(context, CommentId, WarningEntityType.Comment);

        state.LogClosure.Should().Be(WarningLogClosure.PlaceRemoved);
        state.Url.Should().BeNull();
        state.PlaceTitle.Should().Be("Тема", "moderation still reads where the text stood");
    }

    /// <summary>
    /// What moderation deleted stays out of the open log; what its author deleted
    /// is the text the warning was given for and is shown, without an address.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TellDeletionByModerationFromDeletionByTheAuthor(bool byModeration)
    {
        using var context = Seeded();
        var comment = await context.Comments.SingleAsync(c => c.CommentId == CommentId);
        comment.IsRemoved = true;
        comment.DeletedByUserId = byModeration ? ModeratorId : AuthorId;
        var message = await context.Messages.SingleAsync(m => m.MessageId == MessageId);
        message.IsRemoved = true;
        message.DeletedByUserId = byModeration ? ModeratorId : AuthorId;
        await context.SaveChangesAsync();

        var commentState = await Resolve(context, CommentId, WarningEntityType.Comment);
        var messageState = await Resolve(context, MessageId, WarningEntityType.Message);

        var expected = byModeration ? WarningLogClosure.RemovedByModeration : (WarningLogClosure?)null;
        commentState.LogClosure.Should().Be(expected);
        messageState.LogClosure.Should().Be(expected);
        commentState.Url.Should().BeNull("a deleted text has no page to open");
        messageState.Url.Should().BeNull();
        commentState.PlaceTitle.Should().Be("Тема", "the place and the date outlive the deletion");
        commentState.CreatedUtc.Should().Be(Written);
    }

    /// <summary>
    /// A game is read by the rules every game list reads it by, and its
    /// discussion with it: no mode of the discussion shuts it to a reader.
    /// </summary>
    [Theory]
    [InlineData(ModuleStatus.Active, PremoderationStatus.Approved, DraftVisibility.Private,
        CommentsAccessMode.Public, false, null)]
    [InlineData(ModuleStatus.Active, PremoderationStatus.Approved, DraftVisibility.Private,
        CommentsAccessMode.Readonly, false, null)]
    [InlineData(ModuleStatus.Draft, PremoderationStatus.Approved, DraftVisibility.Private,
        CommentsAccessMode.Public, false, WarningLogClosure.PlaceNotPublic)]
    [InlineData(ModuleStatus.Draft, PremoderationStatus.Approved, DraftVisibility.Public,
        CommentsAccessMode.Public, false, null)]
    [InlineData(ModuleStatus.Active, PremoderationStatus.AwaitingApproval, DraftVisibility.Public,
        CommentsAccessMode.Public, false, WarningLogClosure.PlaceNotPublic)]
    [InlineData(ModuleStatus.Active, PremoderationStatus.Approved, DraftVisibility.Public,
        CommentsAccessMode.Public, true, WarningLogClosure.PlaceRemoved)]
    public async Task ReadAGameDiscussionByTheRulesOfTheGame(
        ModuleStatus status,
        PremoderationStatus premoderation,
        DraftVisibility draftVisibility,
        CommentsAccessMode commentsAccess,
        bool removed,
        WarningLogClosure? expected)
    {
        using var context = Seeded();
        context.Games.Add(new DbGame
        {
            GameId = GameId,
            PublicId = "gamew",
            Title = "Тайны Древних",
            MasterId = ModeratorId,
            Status = status,
            PremoderationStatus = premoderation,
            DraftVisibility = draftVisibility,
            CommentsAccessMode = commentsAccess,
            IsRemoved = removed
        });
        context.Comments.Add(new DbComment
        {
            CommentId = GameCommentId,
            EntityId = GameId,
            AuthorId = AuthorId,
            Text = Offending,
            CreatedUtc = Written
        });
        await context.SaveChangesAsync();

        var state = await Resolve(context, GameCommentId, WarningEntityType.Comment);

        state.LogClosure.Should().Be(expected);
        state.PlaceKind.Should().Be(WarningPlaceKind.Game);
        state.PlaceTitle.Should().Be("Тайны Древних");
    }

    /// <summary>
    /// A blog the same way: public when a guest may open it.
    /// </summary>
    [Theory]
    [InlineData(ModuleStatus.Active, PremoderationStatus.Approved, false, null)]
    [InlineData(ModuleStatus.Draft, PremoderationStatus.Approved, false, WarningLogClosure.PlaceNotPublic)]
    [InlineData(ModuleStatus.Active, PremoderationStatus.AwaitingEdits, false, WarningLogClosure.PlaceNotPublic)]
    [InlineData(ModuleStatus.Active, PremoderationStatus.Approved, true, WarningLogClosure.PlaceRemoved)]
    public async Task ReadABlogDiscussionByTheRulesOfTheBlog(
        ModuleStatus status, PremoderationStatus premoderation, bool removed, WarningLogClosure? expected)
    {
        using var context = Seeded();
        context.Blogs.Add(new DbBlog
        {
            BlogId = BlogId,
            PublicId = "blogw",
            Title = "Истории персонажей",
            AuthorId = ModeratorId,
            Status = status,
            PremoderationStatus = premoderation,
            DraftVisibility = DraftVisibility.Private,
            IsRemoved = removed
        });
        context.Comments.Add(new DbComment
        {
            CommentId = BlogCommentId,
            EntityId = BlogId,
            AuthorId = AuthorId,
            Text = Offending,
            CreatedUtc = Written
        });
        await context.SaveChangesAsync();

        var state = await Resolve(context, BlogCommentId, WarningEntityType.Comment);

        state.LogClosure.Should().Be(expected);
        state.PlaceKind.Should().Be(WarningPlaceKind.Blog);
        state.PlaceTitle.Should().Be("Истории персонажей");
    }

    /// <summary>
    /// A comment under a publication is placed at the publication, under its
    /// title, and its address is the comment on the page of the publication. Its
    /// discussion carries the warn button as every discussion does, and the
    /// warning used to name no place at all: the log drew it as a warning issued
    /// from a profile.
    /// </summary>
    /// <remarks>
    /// A guest follows the link where the blog is open to him and the
    /// publication is out; a removed publication or blog takes the link with it.
    /// </remarks>
    [Theory]
    [InlineData(ModuleStatus.Active, true, false, false, null)]
    [InlineData(ModuleStatus.Active, false, false, false, WarningLogClosure.PlaceNotPublic)]
    [InlineData(ModuleStatus.Draft, true, false, false, WarningLogClosure.PlaceNotPublic)]
    [InlineData(ModuleStatus.Active, true, true, false, WarningLogClosure.PlaceRemoved)]
    [InlineData(ModuleStatus.Active, true, false, true, WarningLogClosure.PlaceRemoved)]
    public async Task PlaceACommentUnderAPublicationAtThePublication(
        ModuleStatus blogStatus, bool published, bool publicationRemoved, bool blogRemoved,
        WarningLogClosure? expected)
    {
        using var context = Seeded();
        context.Blogs.Add(new DbBlog
        {
            BlogId = BlogId,
            PublicId = "blogw",
            Title = "Истории персонажей",
            AuthorId = ModeratorId,
            Status = blogStatus,
            PremoderationStatus = PremoderationStatus.Approved,
            DraftVisibility = DraftVisibility.Private,
            IsRemoved = blogRemoved
        });
        context.Publications.Add(new DbPublication
        {
            PublicationId = PublicationId,
            BlogId = BlogId,
            AuthorId = ModeratorId,
            Title = "Как я провел лето",
            IsPublished = published,
            IsRemoved = publicationRemoved,
            CreatedUtc = Written
        });
        context.Comments.Add(new DbComment
        {
            CommentId = PublicationCommentId,
            EntityId = PublicationId,
            AuthorId = AuthorId,
            Text = Offending,
            CreatedUtc = Written
        });
        await context.SaveChangesAsync();

        var state = await Resolve(context, PublicationCommentId, WarningEntityType.Comment);

        state.PlaceKind.Should().Be(WarningPlaceKind.Publication);
        state.PlaceTitle.Should().Be("Как я провел лето");
        state.CreatedUtc.Should().Be(Written);
        state.LogClosure.Should().Be(expected);
        state.Url.Should().Be(publicationRemoved || blogRemoved
            ? null
            : $"/blogs/blogw/feed/{PublicationId}#comment-{PublicationCommentId}");
    }

    /// <summary>
    /// A private conversation and an object nothing finds are closed to the log.
    /// </summary>
    [Fact]
    public async Task CloseTheLogOverWhatNoPlaceHolds()
    {
        using var context = Seeded();

        var direct = await Resolve(context, DirectMessageId, WarningEntityType.Message);
        var missing = await Resolve(context, Guid.NewGuid(), WarningEntityType.Comment);

        direct.LogClosure.Should().Be(WarningLogClosure.NotFound);
        direct.PlaceKind.Should().BeNull();
        missing.LogClosure.Should().Be(WarningLogClosure.NotFound);
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
