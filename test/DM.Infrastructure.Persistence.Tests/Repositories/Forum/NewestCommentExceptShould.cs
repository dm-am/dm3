using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence.Repositories.Blog;
using DM.Infrastructure.Persistence.Repositories.Forum;
using DM.Infrastructure.Persistence.Repositories.Game;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Forum;

/// <summary>
/// The successor of a deleted comment is never the deleted comment, and never a
/// coin toss.
/// </summary>
/// <remarks>
/// When the last comment of a discussion goes, the service asks the repository which
/// comment takes over the pointer. That used to be spelled positionally — order by
/// time descending, skip one — which is the right row only while the comment being
/// deleted sorts first. Two comments can carry the same timestamp (the DM2 import
/// produces them by the thousand, and so does a busy second), and among equals the
/// order was whatever the plan returned: the row being deleted could sort second and
/// be handed back as its own successor.
///
/// The pointer would then name a soft-deleted comment. Every read filters that row
/// out, so the discussion reads as having no last comment at all and falls to the
/// bottom of the activity order — the same symptom as an erased pointer, reached by
/// a different route and only when two comments tie.
///
/// Arrival order is what the defect turned on, so it is what varies here: the same
/// two tied comments seeded each way round must give one answer, and that answer must
/// be the other comment. All four discussions ask the same question of the same table
/// through four repositories, so all four are asked.
/// </remarks>
public class NewestCommentExceptShould
{
    private static readonly Guid EntityId = Guid.Parse("9a41c8d0-0000-4000-8000-000000000001");
    private static readonly Guid AuthorId = Guid.Parse("9a41c8d0-0000-4000-8000-000000000002");

    /// <summary>
    /// Differ in the last byte only, so .NET and Postgres order them the same way, and
    /// Greater is the greater of the two.
    /// </summary>
    private static readonly Guid SmallerId = Guid.Parse("9a41c8d0-0000-4000-8000-000000000011");

    private static readonly Guid GreaterId = Guid.Parse("9a41c8d0-0000-4000-8000-000000000012");

    private static readonly DateTimeOffset Tie = new(2026, 6, 1, 21, 0, 0, TimeSpan.Zero);

    private static DbComment NewComment(Guid id, int minutesAfter = 0) => new()
    {
        CommentId = id,
        EntityId = EntityId,
        AuthorId = AuthorId,
        Text = "Реплика",
        CreatedUtc = Tie.AddMinutes(minutesAfter),
    };

    private static DmDbContext Seeded(params DbComment[] comments)
    {
        var context = new DmDbContext(new DbContextOptionsBuilder<DmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        context.Comments.AddRange(comments);
        context.SaveChanges();
        return context;
    }

    /// <summary>
    /// The same question through each of the four repositories. They read one table and
    /// differ only in which entity the comments hang off, so a defect fixed in one and
    /// left in another is invisible until a user hits that discussion.
    /// </summary>
    public static TheoryData<string> Discussions() =>
        new("topic", "blog", "publication", "game");

    private static Task<Guid?> AskAsync(string discussion, DmDbContext context, Guid deleted) =>
        discussion switch
        {
            "topic" => new TopicCommentRepository(context, null!, null!, null!)
                .GetNewestCommentIdExcept(EntityId, deleted),
            "blog" => new BlogCommentRepository(context, null!, null!)
                .GetNewestCommentIdExcept(EntityId, deleted),
            "publication" => new PublicationCommentRepository(context, null!, null!)
                .GetNewestCommentIdExcept(EntityId, deleted),
            _ => new GameCommentRepository(context, null!)
                .GetNewestCommentIdExcept(EntityId, deleted),
        };

    /// <summary>
    /// The pointer names the smaller identifier of two tied comments, which is the case
    /// the positional form got wrong: the deleted row sorts second, and skipping one
    /// lands on it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Discussions))]
    public async Task NeverHandBackTheCommentThatIsLeaving(string discussion)
    {
        using var smallerFirst = Seeded(NewComment(SmallerId), NewComment(GreaterId));
        using var greaterFirst = Seeded(NewComment(GreaterId), NewComment(SmallerId));

        var fromSmallerFirst = await AskAsync(discussion, smallerFirst, SmallerId);
        var fromGreaterFirst = await AskAsync(discussion, greaterFirst, SmallerId);

        fromSmallerFirst.Should().Be(GreaterId,
            "the comment being deleted cannot succeed itself — the pointer would name a row " +
            "every read filters out, and the discussion would read as having no comments");
        fromGreaterFirst.Should().Be(GreaterId,
            "and the answer does not depend on which of the two tied rows the plan returns first");
    }

    /// <summary>
    /// Two comments left, tied with each other: which one takes the pointer is decided
    /// by the identifier and not by the plan.
    /// </summary>
    /// <remarks>
    /// The fact above needs the exclusion and nothing else — with one survivor there is
    /// only one answer to give — so it stays green with the tie-breaker deleted, and for
    /// a while that is what the tie-breaker had holding it: nothing. It takes three tied
    /// comments to ask the question. Without ordering by the identifier the answer is
    /// whichever of the two the provider happens to return first, so the same pair
    /// seeded both ways round gives two different pointers for one deletion — and a
    /// pointer that changes with the plan is a discussion whose activity date changes
    /// with the plan.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Discussions))]
    public async Task DecideBetweenTiedSurvivorsByTheIdentifier(string discussion)
    {
        var deleted = Guid.Parse("9a41c8d0-0000-4000-8000-000000000013");

        using var smallerFirst = Seeded(
            NewComment(SmallerId), NewComment(GreaterId), NewComment(deleted));
        using var greaterFirst = Seeded(
            NewComment(GreaterId), NewComment(SmallerId), NewComment(deleted));

        var fromSmallerFirst = await AskAsync(discussion, smallerFirst, deleted);
        var fromGreaterFirst = await AskAsync(discussion, greaterFirst, deleted);

        fromSmallerFirst.Should().Be(GreaterId,
            "among comments carrying the same timestamp the greater identifier is the newer " +
            "one this repository names, and it must not depend on insertion order");
        fromGreaterFirst.Should().Be(GreaterId,
            "the same two comments seeded the other way round are still the same two comments");
    }

    [Theory]
    [MemberData(nameof(Discussions))]
    public async Task PickTheNewerOfTheRemainingComments(string discussion)
    {
        using var context = Seeded(
            NewComment(SmallerId),
            NewComment(GreaterId, 5),
            NewComment(Guid.Parse("9a41c8d0-0000-4000-8000-000000000013"), 10));

        var successor = await AskAsync(discussion, context, Guid.Parse("9a41c8d0-0000-4000-8000-000000000013"));

        successor.Should().Be(GreaterId,
            "time still decides when the timestamps differ; the identifier only speaks among equals");
    }

    [Theory]
    [MemberData(nameof(Discussions))]
    public async Task SkipTheCommentsAlreadyDeleted(string discussion)
    {
        var removed = NewComment(GreaterId, 5);
        removed.IsRemoved = true;
        using var context = Seeded(NewComment(SmallerId), removed);

        var successor = await AskAsync(discussion, context, Guid.Parse("9a41c8d0-0000-4000-8000-000000000013"));

        successor.Should().Be(SmallerId,
            "a comment somebody already deleted is not a comment the pointer may name");
    }

    [Theory]
    [MemberData(nameof(Discussions))]
    public async Task AnswerNothingWhenTheDeletedOneWasTheOnlyComment(string discussion)
    {
        using var context = Seeded(NewComment(SmallerId));

        var successor = await AskAsync(discussion, context, SmallerId);

        successor.Should().BeNull(
            "nothing is left to name, which is the one case the null means what it says");
    }
}
