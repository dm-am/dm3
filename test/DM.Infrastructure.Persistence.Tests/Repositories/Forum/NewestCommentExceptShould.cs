using System;
using System.Threading.Tasks;
using DM.Infrastructure.Persistence.Shared.Comments;
using AwesomeAssertions;
using Xunit;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Forum;

/// <summary>
/// The successor of a deleted comment is never the deleted comment, and never a
/// coin toss.
/// </summary>
/// <remarks>
/// The successor is chosen by exclusion and identifier, never by position: among tied timestamps the deleted
/// row could sort second. All four discussions ask the shared query.
/// </remarks>
public class NewestCommentExceptShould
{
    private static readonly Guid EntityId = Guid.Parse("9a41c8d0-0000-4000-8000-000000000001");
    private static readonly Guid AuthorId = Guid.Parse("9a41c8d0-0000-4000-8000-000000000002");

    // Differ in the last byte only, so .NET and Postgres order them the same way, and Greater is
    // the greater of the two.
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
        var context = InMemoryStore.Open();
        context.Comments.AddRange(comments);
        context.SaveChanges();
        return context;
    }

    public static TheoryData<string> Discussions() =>
        new("topic", "blog", "publication", "game");

    private static Task<Guid?> AskAsync(string discussion, DmDbContext context, Guid deleted) =>
        discussion switch
        {
            // All four ask it inside the transaction that marks the comment, and that
            // transaction is set-based statements with no in-memory implementation, so
            // the query is asked here in the words each of them asks it in.
            "topic" => CommentQueries.NewestExcept(
                context, EntityId, deleted, "DM.TopicComments.NewestCommentIdExcept"),
            "blog" => CommentQueries.NewestExcept(
                context, EntityId, deleted, "DM.BlogComments.NewestCommentIdExcept"),
            "publication" => CommentQueries.NewestExcept(
                context, EntityId, deleted, "DM.PublicationComments.NewestCommentIdExcept"),
            _ => CommentQueries.NewestExcept(
                context, EntityId, deleted, "DM.GameComments.NewestCommentIdExcept"),
        };

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
    /// <remarks>It takes three tied comments to ask the tie-breaker; seeded both ways they give one pointer.</remarks>
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
