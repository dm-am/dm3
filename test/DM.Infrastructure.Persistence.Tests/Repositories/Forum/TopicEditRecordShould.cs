using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Forum.Features.Topics;
using DM.Infrastructure.Persistence.Repositories.Forum;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;
using DbBoard = DM.Infrastructure.Persistence.Entities.Forum.Board;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Forum;

/// <summary>
/// Who changed a topic, and whether anything changed at all.
/// </summary>
/// <remarks>
/// TopicEdits is the one home of who changed a topic. A PATCH with the values held writes no history and
/// announces nothing; both are read off the change tracker.
/// </remarks>
public class TopicEditRecordShould
{
    private static readonly Guid BoardId = Guid.Parse("6b1f0a10-0000-4000-8000-000000000001");
    private static readonly Guid TopicId = Guid.Parse("6b1f0a10-0000-4000-8000-000000000002");
    private static readonly Guid AuthorId = Guid.Parse("6b1f0a10-0000-4000-8000-000000000003");
    private static readonly Guid EditorId = Guid.Parse("6b1f0a10-0000-4000-8000-000000000004");
    private static readonly Guid EditRecordId = Guid.Parse("6b1f0a10-0000-4000-8000-000000000005");

    private const string OriginalTitle = "Тема до правки";

    private static readonly DateTimeOffset Created = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EditedAt = new(2026, 5, 2, 9, 30, 0, TimeSpan.Zero);

    private static DmDbContext Seeded()
    {
        // The in-memory store has no transaction, so its warning about ignoring one is suppressed.
        var context = new DmDbContext(new DbContextOptionsBuilder<DmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

        context.Users.AddRange(
            User(AuthorId, "author"),
            User(EditorId, "moderator"));

        context.Boards.Add(new DbBoard
        {
            BoardId = BoardId,
            Title = "Раздел",
            Alias = "razdel",
            Order = 1
        });

        context.Topics.Add(new DbTopic
        {
            TopicId = TopicId,
            BoardId = BoardId,
            AuthorId = AuthorId,
            Title = OriginalTitle,
            Text = "Текст темы.",
            TopicNumber = 1,
            CreatedUtc = Created
        });

        context.SaveChanges();
        return context;
    }

    private static TopicRepository RepositoryOver(DmDbContext context)
    {
        var guids = Substitute.For<IGuidFactory>();
        guids.Create().Returns(EditRecordId);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.Now.Returns(EditedAt);

        return new TopicRepository(context, guids, clock);
    }

    /// <summary>
    /// The editor is written down, and the answer says the row moved.
    /// </summary>
    [Fact]
    public async Task RecordWhoChangedTheTopic()
    {
        using var context = Seeded();

        var result = await RepositoryOver(context).Update(new UpdateTopicEntity
        {
            TopicId = TopicId,
            Title = "Тема после правки",
            EditorUserId = EditorId
        });

        result.Changed.Should().BeTrue("the title is not the one the topic held");

        var edits = await context.TopicEdits.AsNoTracking()
            .Where(e => e.TopicId == TopicId).ToListAsync();
        edits.Should().ContainSingle("one request that changed the topic is one edit");
        edits[0].EditorUserId.Should().Be(EditorId,
            "the history is the only place the schema keeps who changed a topic");
        edits[0].EditorUserId.Should().NotBe(AuthorId,
            "editing is open to moderators, so the editor is not the author");
        edits[0].EditedUtc.Should().Be(EditedAt);
    }

    /// <summary>
    /// A save that changes nothing leaves nothing behind, and says so.
    /// </summary>
    /// <remarks>The client PATCHes the fields it holds on every save, cancelled ones included.</remarks>
    [Fact]
    public async Task LeaveNoRecordWhenTheValuesAreTheOnesTheTopicAlreadyHolds()
    {
        using var context = Seeded();

        var result = await RepositoryOver(context).Update(new UpdateTopicEntity
        {
            TopicId = TopicId,
            Title = OriginalTitle,
            EditorUserId = EditorId
        });

        result.Changed.Should().BeFalse("no column of the topic moved");
        (await context.TopicEdits.AsNoTracking().CountAsync()).Should().Be(0);
    }

    private static DbUser User(Guid id, string username) => new()
    {
        UserId = id,
        Username = username,
        Email = username + "@test.local",
        Salt = string.Empty,
        PasswordHash = string.Empty,
        Role = UserRole.RegularUser,
        CreatedUtc = Created
    };
}
