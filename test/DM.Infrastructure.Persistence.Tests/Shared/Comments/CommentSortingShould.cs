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

// Each page asks for the order again, and equal keys are ordinary (the DM2 import, a busy second,
// no likes).
public class CommentSortingShould
{
    private static readonly Guid EntityId = Guid.Parse("6f0a1c40-0000-4000-8000-000000000001");
    private static readonly Guid AuthorId = Guid.Parse("6f0a1c40-0000-4000-8000-000000000002");

    private static readonly DateTimeOffset Tie = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    // Differ in the last byte only, so the order between them is the same in .NET and in Postgres.
    private static readonly Guid[] CommentIds =
    [
        Guid.Parse("6f0a1c40-0000-4000-8000-000000000011"),
        Guid.Parse("6f0a1c40-0000-4000-8000-000000000012"),
        Guid.Parse("6f0a1c40-0000-4000-8000-000000000013"),
        Guid.Parse("6f0a1c40-0000-4000-8000-000000000014")
    ];

    private static DmDbContext Seeded(IEnumerable<Guid> commentIds)
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
