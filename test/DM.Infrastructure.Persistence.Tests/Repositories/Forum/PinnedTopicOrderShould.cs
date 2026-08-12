using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Repositories.Forum;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using DbBoard = DM.Infrastructure.Persistence.Entities.Forum.Board;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Forum;

/// <summary>
/// The pinned order of a board is written whole, and only for that board.
/// </summary>
/// <remarks>
/// The write used to be keyed by topic id alone: it took a map of id to position
/// and updated whatever rows carried those ids, so the board only ever entered
/// the call through the permission check made before it. Both halves of that are
/// pinned here. Whose topics move is decided by the board of the address, not by
/// the ids in the body, or the moderator of one board renumbers the pinned
/// topics of another by naming them. And the read the service validates a body
/// against is scoped the same way, since it is what decides whether a body
/// covers the board or only part of it.
/// </remarks>
public class PinnedTopicOrderShould
{
    private static readonly Guid BoardId = Guid.Parse("9a4c1d20-0000-4000-8000-000000000001");
    private static readonly Guid OtherBoardId = Guid.Parse("9a4c1d20-0000-4000-8000-000000000002");
    private static readonly Guid AuthorId = Guid.Parse("9a4c1d20-0000-4000-8000-000000000003");

    private static readonly Guid First = Guid.Parse("9a4c1d20-0000-4000-8000-000000000011");
    private static readonly Guid Second = Guid.Parse("9a4c1d20-0000-4000-8000-000000000012");
    private static readonly Guid Third = Guid.Parse("9a4c1d20-0000-4000-8000-000000000013");
    private static readonly Guid Loose = Guid.Parse("9a4c1d20-0000-4000-8000-000000000014");
    private static readonly Guid Foreign = Guid.Parse("9a4c1d20-0000-4000-8000-000000000015");

    /// <summary>The position the other board's pinned topic holds, and keeps.</summary>
    private const int ForeignOrder = 7;

    private static readonly DateTimeOffset Created = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly IMapper Mapper = new MapperConfiguration(cfg =>
        cfg.AddMaps(typeof(TopicMappingProfile).Assembly)).CreateMapper();

    /// <summary>
    /// Two boards. The first holds three pinned topics and one that is not
    /// pinned; the second holds a pinned topic nobody in the first may touch.
    /// </summary>
    private static DmDbContext Seeded()
    {
        var context = new DmDbContext(new DbContextOptionsBuilder<DmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        context.Users.Add(new DbUser
        {
            UserId = AuthorId,
            Username = "author",
            Email = "author@test.local",
            Salt = string.Empty,
            PasswordHash = string.Empty,
            Role = UserRole.RegularUser,
            CreatedUtc = Created
        });

        context.Boards.AddRange(
            NewBoard(BoardId, "Раздел", "razdel", 1),
            NewBoard(OtherBoardId, "Соседний раздел", "sosedniy", 2));

        context.Topics.AddRange(
            NewTopic(First, BoardId, 1, attachOrder: 0),
            NewTopic(Second, BoardId, 2, attachOrder: 1),
            NewTopic(Third, BoardId, 3, attachOrder: 2),
            NewTopic(Loose, BoardId, 4, attachOrder: null, isAttached: false),
            NewTopic(Foreign, OtherBoardId, 1, attachOrder: ForeignOrder));

        context.SaveChanges();
        return context;
    }

    private static TopicRepository RepositoryOver(DmDbContext context) => new(
        context, Mapper, new Mock<IGuidFactory>().Object, new Mock<IDateTimeProvider>().Object);

    [Fact]
    public async Task ListThePinnedTopicsOfTheBoardItWasAskedAbout()
    {
        using var context = Seeded();

        var pinned = await RepositoryOver(context).GetAttachedTopicIds(BoardId);

        pinned.Should().Equal(new[] { First, Second, Third },
            "the service checks the body against this list, so a pinned topic missing from " +
            "it would be refused and a topic of another board would be demanded");
    }

    [Fact]
    public async Task GiveEveryPinnedTopicThePositionTheBodyPutItIn()
    {
        using var context = Seeded();

        await RepositoryOver(context).ReplaceAttachOrder(BoardId, new[] { Third, First, Second });

        var order = await context.Topics.AsNoTracking()
            .Where(t => t.BoardId == BoardId && t.IsAttached)
            .OrderBy(t => t.AttachOrder)
            .Select(t => t.TopicId)
            .ToListAsync();

        order.Should().Equal(Third, First, Second);
    }

    [Fact]
    public async Task LeaveTheOtherBoardsPinnedTopicWhereItWas()
    {
        using var context = Seeded();

        // A body addressed to one board and naming a topic pinned in another: the
        // address is what decides whose topics move.
        await RepositoryOver(context)
            .ReplaceAttachOrder(BoardId, new[] { Foreign, Third, First, Second });

        var foreign = await context.Topics.AsNoTracking().SingleAsync(t => t.TopicId == Foreign);
        foreign.AttachOrder.Should().Be(ForeignOrder);
    }

    private static DbBoard NewBoard(Guid id, string title, string alias, int order) => new()
    {
        BoardId = id,
        Title = title,
        Alias = alias,
        Order = order
    };

    private static DbTopic NewTopic(
        Guid id, Guid boardId, int number, int? attachOrder, bool isAttached = true) => new()
    {
        TopicId = id,
        BoardId = boardId,
        AuthorId = AuthorId,
        Title = "Топик " + number,
        Text = "Текст топика.",
        TopicNumber = number,
        CreatedUtc = Created,
        IsAttached = isAttached,
        AttachOrder = attachOrder
    };
}
