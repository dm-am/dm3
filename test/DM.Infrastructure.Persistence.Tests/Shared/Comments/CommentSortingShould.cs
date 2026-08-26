using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Shared.Comments;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbLike = DM.Infrastructure.Persistence.Entities.Shared.Like;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Tests.Shared.Comments;

/// <summary>
/// Two pages of one discussion are two halves of the same list, not two lists.
/// </summary>
/// <remarks>
/// The order is asked for once per page, and a page is a range of it. When the keys of
/// that order repeat across rows, nothing says the two asks arrange those rows the same
/// way: a comment that moved between them lands on both pages, and the one that took its
/// place lands on neither. Equal keys are ordinary here, not exotic. The DM2 import gives
/// thousands of comments one timestamp, a busy second does the same, and sorting by likes
/// ties every comment nobody has liked yet.
///
/// Arrival order is what the defect hangs on, so it is what varies between the two halves
/// of the assertion: the same four comments, seeded in one order and then in the other,
/// have to page identically. An order settled by the identifier answers both the same way;
/// one settled by nothing answers each way once.
/// </remarks>
public class CommentSortingShould
{
    private static readonly Guid EntityId = Guid.Parse("6f0a1c40-0000-4000-8000-000000000001");
    private static readonly Guid AuthorId = Guid.Parse("6f0a1c40-0000-4000-8000-000000000002");

    /// <summary>The one second all four comments were posted in.</summary>
    private static readonly DateTimeOffset Tie = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Differ in the last byte only, so the order between them is the same in .NET and in
    /// Postgres.
    /// </summary>
    private static readonly Guid[] CommentIds =
    [
        Guid.Parse("6f0a1c40-0000-4000-8000-000000000011"),
        Guid.Parse("6f0a1c40-0000-4000-8000-000000000012"),
        Guid.Parse("6f0a1c40-0000-4000-8000-000000000013"),
        Guid.Parse("6f0a1c40-0000-4000-8000-000000000014")
    ];

    /// <summary>
    /// One discussion, four comments sharing a timestamp and a like each, seeded in the
    /// order the caller asks for.
    /// </summary>
    private static DmDbContext Seeded(IEnumerable<Guid> commentIds)
    {
        var context = new DmDbContext(new DbContextOptionsBuilder<DmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

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

        foreach (var commentId in commentIds)
        {
            context.Comments.Add(new DbComment
            {
                CommentId = commentId,
                EntityId = EntityId,
                AuthorId = AuthorId,
                Text = "Реплика",
                CreatedUtc = Tie,
            });

            // One like each, so the like order ties on every row the way the created
            // order does.
            context.Likes.Add(new DbLike
            {
                LikeId = Guid.NewGuid(),
                EntityId = commentId,
                EntityType = LikeEntityType.Comment,
                UserId = AuthorId,
            });
        }

        context.SaveChanges();
        return context;
    }

    /// <summary>Every page of the discussion, read one after another.</summary>
    private static IReadOnlyList<Guid> PagedThrough(DmDbContext context, string? sortBy, int pageSize)
    {
        var read = new List<Guid>();
        for (var skipped = 0; skipped < CommentIds.Length; skipped += pageSize)
        {
            read.AddRange(CommentSorting
                .Apply(context.Comments.Where(c => c.EntityId == EntityId), sortBy, "desc", context)
                .Skip(skipped)
                .Take(pageSize)
                .Select(c => c.CommentId)
                .ToArray());
        }

        return read;
    }

    [Theory]
    [InlineData("created")]
    [InlineData("likes")]
    [InlineData(null)]
    public void PageTheSameWayWhicheverOrderTheRowsArriveIn(string? sortBy)
    {
        using var arrivedInOrder = Seeded(CommentIds);
        using var arrivedReversed = Seeded(CommentIds.Reverse());

        var first = PagedThrough(arrivedInOrder, sortBy, pageSize: 2);
        var second = PagedThrough(arrivedReversed, sortBy, pageSize: 2);

        first.Should().OnlyHaveUniqueItems(
            "a comment that appears on two pages is a comment that pushed another one out of " +
            "the list entirely");
        first.Should().Equal(second,
            "the four comments tie on every key the reader chose, so without a key of their own " +
            "the order is whatever arrangement the rows happened to arrive in");
    }

    /// <summary>
    /// The identifier is a tie-break and not the order: a genuinely newer comment leads
    /// even when its identifier is the smaller one.
    /// </summary>
    [Fact]
    public void StillPreferTheNewerCommentWhenTheTimestampsDiffer()
    {
        using var context = Seeded(CommentIds);
        var newest = context.Comments.Single(c => c.CommentId == CommentIds[0]);
        newest.CreatedUtc = Tie.AddMinutes(1);
        context.SaveChanges();

        var read = PagedThrough(context, "created", pageSize: 4);

        read[0].Should().Be(CommentIds[0],
            "time decides first, and the identifier only speaks when time has nothing to say");
    }
}
