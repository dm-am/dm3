using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Repositories.Forum;
using AwesomeAssertions;
using Xunit;
using DbBoard = DM.Infrastructure.Persistence.Entities.Forum.Board;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Forum;

// A per-group "top 1" does not translate, so the second statement can return several rows for a
// board; equal timestamps are common after the DM2 import.
public class BoardLastCommentTieShould
{
    private static readonly Guid BoardId = Guid.Parse("2d7b6e30-0000-4000-8000-000000000001");
    private static readonly Guid TopicId = Guid.Parse("2d7b6e30-0000-4000-8000-000000000002");
    private static readonly Guid AuthorId = Guid.Parse("2d7b6e30-0000-4000-8000-000000000003");

    // Differ in the last byte only, so the order between them is the same in .NET and in Postgres,
    // and Later is the greater of the two.
    private static readonly Guid EarlierId = Guid.Parse("2d7b6e30-0000-4000-8000-000000000011");

    private static readonly Guid LaterId = Guid.Parse("2d7b6e30-0000-4000-8000-000000000012");

    private static readonly DateTimeOffset Tie = new(2026, 5, 1, 18, 30, 0, TimeSpan.Zero);

    private static DbComment NewComment(Guid id) => new()
    {
        CommentId = id,
        EntityId = TopicId,
        AuthorId = AuthorId,
        Text = "Реплика",
        CreatedUtc = Tie,
    };

    private static DmDbContext Seeded(params Guid[] commentIds)
    {
        var context = InMemoryStore.Open();

        context.Users.Add(new DbUser
        {
            UserId = AuthorId,
            Username = "commenter",
            Email = "commenter@test.local",
            Salt = "",
            PasswordHash = "",
            Role = UserRole.RegularUser,
            CreatedUtc = Tie,
        });

        context.Boards.Add(new DbBoard
        {
            BoardId = BoardId,
            Title = "Раздел",
            Alias = "razdel",
            Order = 1,
        });

        context.Topics.Add(new DbTopic
        {
            TopicId = TopicId,
            BoardId = BoardId,
            AuthorId = AuthorId,
            Title = "Тема",
            Text = "Первое сообщение",
            TopicNumber = 1,
            CreatedUtc = Tie,
        });

        context.Comments.AddRange(commentIds.Select(NewComment));
        context.SaveChanges();
        return context;
    }

    private static async Task<Guid?> LastCommentIdAsync(params Guid[] commentIds)
    {
        using var context = Seeded(commentIds);
        var boards = await new BoardRepository(context).SelectBoards();
        return boards.Single().LastComment?.Id;
    }

    [Fact]
    public async Task NameTheSameCommentWhicheverOrderTheRowsArriveIn()
    {
        var seededEarlierFirst = await LastCommentIdAsync(EarlierId, LaterId);
        var seededLaterFirst = await LastCommentIdAsync(LaterId, EarlierId);

        seededEarlierFirst.Should().NotBeNull("the board has comments, so it has a last one");
        seededEarlierFirst.Should().Be(seededLaterFirst,
            "the two comments carry one timestamp, so the timestamp cannot choose between them — " +
            "without a second key the answer is whatever order the rows came back in, and the " +
            "column flips between two renders of a board nobody touched");
    }

    [Fact]
    public async Task SettleTheTieOnTheGreaterIdentifier()
    {
        (await LastCommentIdAsync(EarlierId, LaterId)).Should().Be(LaterId);
        (await LastCommentIdAsync(LaterId, EarlierId)).Should().Be(LaterId);
    }

    [Fact]
    public async Task StillPreferTheNewerCommentWhenTheTimestampsDiffer()
    {
        using var context = Seeded(LaterId);
        var newest = NewComment(EarlierId);
        newest.CreatedUtc = Tie.AddMinutes(1);
        context.Comments.Add(newest);
        await context.SaveChangesAsync();

        var boards = await new BoardRepository(context).SelectBoards();

        boards.Single().LastComment!.Id.Should().Be(EarlierId,
            "time decides first, and the identifier only speaks when time has nothing to say");
    }
}
