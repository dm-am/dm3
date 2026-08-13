using System;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Forum.Features.Comments;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.Repositories.Blog;
using DM.Infrastructure.Persistence.Repositories.Forum;
using DM.Infrastructure.Persistence.Repositories.Game;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;

namespace DM.Infrastructure.Persistence.Tests.Repositories.Forum;

/// <summary>
/// Deleting a comment from the middle of a discussion leaves the last-comment
/// pointer where it is.
/// </summary>
/// <remarks>
/// The service computes NewLastCommentId only for the comment the pointer names —
/// for every other it hands the repository a null, meaning "nothing to move". Three
/// of the four repositories assigned it anyway, so deleting any earlier comment
/// erased the pointer. The topic list reads a topic's activity through that pointer
/// (Topic.LastComment.CreatedUtc, falling back to the topic's own creation date), so
/// an erased pointer sent a live discussion to the bottom of the activity order until
/// somebody commented again; on a blog and a publication it also made the next
/// deletion believe that no comment is the last one.
///
/// The fourth repository, GameCommentRepository, already carried the guard when the
/// other three were fixed — and was left out of this class for exactly that reason,
/// which made it the one branch where removing the guard again cost nothing: the
/// whole tier stayed green. All four are checked here now. A rule that holds in four
/// places and is asserted in three is asserted in three.
/// </remarks>
public class LastCommentPointerShould
{
    private static readonly DateTimeOffset Created = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid EntityId = Guid.Parse("7c2e1f22-0000-4000-8000-000000000001");
    private static readonly Guid AuthorId = Guid.Parse("7c2e1f22-0000-4000-8000-000000000002");
    private static readonly Guid FirstCommentId = Guid.Parse("7c2e1f22-0000-4000-8000-000000000011");
    private static readonly Guid LastCommentId = Guid.Parse("7c2e1f22-0000-4000-8000-000000000012");

    private static DmDbContext Context() => new(new DbContextOptionsBuilder<DmDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static DbComment NewComment(Guid id, int minutesAfter) => new()
    {
        CommentId = id,
        EntityId = EntityId,
        AuthorId = AuthorId,
        Text = "Реплика",
        CreatedUtc = Created.AddMinutes(minutesAfter),
        IsRemoved = false,
    };

    /// <summary>
    /// Two comments, the pointer on the newer one — the state every discussion is in.
    /// </summary>
    private static DmDbContext Seeded(Action<DmDbContext> addOwner)
    {
        var context = Context();
        context.Comments.AddRange(NewComment(FirstCommentId, 0), NewComment(LastCommentId, 10));
        addOwner(context);
        context.SaveChanges();
        return context;
    }

    private static void AddTopic(DmDbContext context) => context.Topics.Add(new DbTopic
    {
        TopicId = EntityId,
        BoardId = Guid.NewGuid(),
        AuthorId = AuthorId,
        Title = "Тема",
        Text = "Первое сообщение",
        TopicNumber = 1,
        CreatedUtc = Created,
        LastCommentId = LastCommentId,
    });

    private static void AddBlog(DmDbContext context) => context.Blogs.Add(new DbBlog
    {
        BlogId = EntityId,
        PublicId = "blogb",
        AuthorId = AuthorId,
        Title = "Блог",
        CreatedUtc = Created,
        CommentCount = 2,
        LastCommentId = LastCommentId,
    });

    private static void AddPublication(DmDbContext context) => context.Publications.Add(new DbPublication
    {
        PublicationId = EntityId,
        BlogId = Guid.NewGuid(),
        AuthorId = AuthorId,
        PublicationNumber = 1,
        Title = "Запись",
        CreatedUtc = Created,
        CommentCount = 2,
        LastCommentId = LastCommentId,
    });

    private static void AddGame(DmDbContext context) => context.Games.Add(new DbGame
    {
        GameId = EntityId,
        PublicId = "gameg",
        MasterId = AuthorId,
        Title = "Игра",
        CreatedUtc = Created,
        CommentCount = 2,
        LastCommentId = LastCommentId,
    });

    private static TopicCommentRepository Topics(DmDbContext context) =>
        new(context, null!, null!, null!);

    private static BlogCommentRepository Blogs(DmDbContext context) =>
        new(context, null!, null!, null!);

    private static PublicationCommentRepository Publications(DmDbContext context) =>
        new(context, null!, null!, null!);

    private static GameCommentRepository Games(DmDbContext context) =>
        new(context, null!, null!);

    [Fact]
    public async Task StayOnTheTopicWhenAnEarlierCommentGoes()
    {
        using var context = Seeded(AddTopic);

        await Topics(context).Delete(new DeleteTopicCommentEntity
        {
            CommentId = FirstCommentId,
            TopicId = EntityId,
            NewLastCommentId = null,
            DeletedByUserId = AuthorId,
            DeletedUtc = Created.AddHours(1),
        });

        (await context.Topics.FindAsync(EntityId))!.LastCommentId.Should().Be(LastCommentId,
            "the deleted comment is not the one the pointer names, so there is nothing to move");
    }

    [Fact]
    public async Task MoveToTheSecondLastWhenTheTopicLosesItsLastComment()
    {
        using var context = Seeded(AddTopic);

        await Topics(context).Delete(new DeleteTopicCommentEntity
        {
            CommentId = LastCommentId,
            TopicId = EntityId,
            NewLastCommentId = FirstCommentId,
            DeletedByUserId = AuthorId,
            DeletedUtc = Created.AddHours(1),
        });

        (await context.Topics.FindAsync(EntityId))!.LastCommentId.Should().Be(FirstCommentId,
            "the pointer names the newest comment left, and that is the second last one");
    }

    [Fact]
    public async Task EmptyWhenTheTopicLosesItsOnlyComment()
    {
        using var context = Seeded(AddTopic);

        await Topics(context).Delete(new DeleteTopicCommentEntity
        {
            CommentId = LastCommentId,
            TopicId = EntityId,
            NewLastCommentId = null,
            DeletedByUserId = AuthorId,
            DeletedUtc = Created.AddHours(1),
        });

        (await context.Topics.FindAsync(EntityId))!.LastCommentId.Should().BeNull(
            "the pointer named the deleted comment and nothing is left to name, which is the " +
            "one case the null means what it says");
    }

    [Fact]
    public async Task StayOnTheBlogWhenAnEarlierCommentGoes()
    {
        using var context = Seeded(AddBlog);

        await Blogs(context).Delete(new DeleteBlogCommentEntity
        {
            CommentId = FirstCommentId,
            BlogId = EntityId,
            NewCommentCount = 1,
            NewLastCommentId = null,
            DeletedByUserId = AuthorId,
            DeletedUtc = Created.AddHours(1),
        });

        var blog = (await context.Blogs.FindAsync(EntityId))!;
        blog.LastCommentId.Should().Be(LastCommentId,
            "the deleted comment is not the one the pointer names");
        blog.CommentCount.Should().Be(1, "the count is recomputed by the caller either way");
    }

    [Fact]
    public async Task MoveOnTheBlogWhenItsLastCommentGoes()
    {
        using var context = Seeded(AddBlog);

        await Blogs(context).Delete(new DeleteBlogCommentEntity
        {
            CommentId = LastCommentId,
            BlogId = EntityId,
            NewCommentCount = 1,
            NewLastCommentId = FirstCommentId,
            DeletedByUserId = AuthorId,
            DeletedUtc = Created.AddHours(1),
        });

        (await context.Blogs.FindAsync(EntityId))!.LastCommentId.Should().Be(FirstCommentId);
    }

    /// <summary>
    /// The guard has two halves and this is the second one.
    /// </summary>
    /// <remarks>
    /// "Move it when the service named a successor" is one condition; "clear it when
    /// the row it points at is the one going away" is the other, and only the second
    /// answers the last comment of a discussion. Written for the blog and the
    /// publication because the topic already had it and the two of them did not: a
    /// guard cut down to <c>if (NewLastCommentId.HasValue)</c> passed every other test
    /// in this class while leaving the pointer on a soft-deleted row — which the
    /// global filter then reads as no comment at all, one query later.
    /// </remarks>
    [Fact]
    public async Task EmptyWhenTheBlogLosesItsOnlyComment()
    {
        using var context = Seeded(AddBlog);

        await Blogs(context).Delete(new DeleteBlogCommentEntity
        {
            CommentId = LastCommentId,
            BlogId = EntityId,
            NewCommentCount = 0,
            NewLastCommentId = null,
            DeletedByUserId = AuthorId,
            DeletedUtc = Created.AddHours(1),
        });

        (await context.Blogs.FindAsync(EntityId))!.LastCommentId.Should().BeNull(
            "the pointer named the deleted comment and nothing is left to name — leaving it " +
            "there points the blog at a row every read filters out");
    }

    [Fact]
    public async Task StayOnThePublicationWhenAnEarlierCommentGoes()
    {
        using var context = Seeded(AddPublication);

        await Publications(context).Delete(new DeletePublicationCommentEntity
        {
            CommentId = FirstCommentId,
            PublicationId = EntityId,
            NewCommentCount = 1,
            NewLastCommentId = null,
            DeletedByUserId = AuthorId,
            DeletedUtc = Created.AddHours(1),
        });

        (await context.Publications.FindAsync(EntityId))!.LastCommentId.Should().Be(LastCommentId,
            "the deleted comment is not the one the pointer names");
    }

    [Fact]
    public async Task MoveOnThePublicationWhenItsLastCommentGoes()
    {
        using var context = Seeded(AddPublication);

        await Publications(context).Delete(new DeletePublicationCommentEntity
        {
            CommentId = LastCommentId,
            PublicationId = EntityId,
            NewCommentCount = 1,
            NewLastCommentId = FirstCommentId,
            DeletedByUserId = AuthorId,
            DeletedUtc = Created.AddHours(1),
        });

        (await context.Publications.FindAsync(EntityId))!.LastCommentId.Should().Be(FirstCommentId);
    }

    /// <inheritdoc cref="EmptyWhenTheBlogLosesItsOnlyComment" />
    [Fact]
    public async Task EmptyWhenThePublicationLosesItsOnlyComment()
    {
        using var context = Seeded(AddPublication);

        await Publications(context).Delete(new DeletePublicationCommentEntity
        {
            CommentId = LastCommentId,
            PublicationId = EntityId,
            NewCommentCount = 0,
            NewLastCommentId = null,
            DeletedByUserId = AuthorId,
            DeletedUtc = Created.AddHours(1),
        });

        (await context.Publications.FindAsync(EntityId))!.LastCommentId.Should().BeNull(
            "the pointer named the deleted comment and nothing is left to name");
    }

    [Fact]
    public async Task StayOnTheGameWhenAnEarlierCommentGoes()
    {
        using var context = Seeded(AddGame);

        await Games(context).Delete(new DeleteGameCommentEntity
        {
            CommentId = FirstCommentId,
            GameId = EntityId,
            NewCommentCount = 1,
            NewLastCommentId = null,
            DeletedByUserId = AuthorId,
            DeletedUtc = Created.AddHours(1),
        });

        var game = (await context.Games.FindAsync(EntityId))!;
        game.LastCommentId.Should().Be(LastCommentId,
            "the deleted comment is not the one the pointer names");
        game.CommentCount.Should().Be(1, "the count is recomputed by the caller either way");
    }

    [Fact]
    public async Task MoveOnTheGameWhenItsLastCommentGoes()
    {
        using var context = Seeded(AddGame);

        await Games(context).Delete(new DeleteGameCommentEntity
        {
            CommentId = LastCommentId,
            GameId = EntityId,
            NewCommentCount = 1,
            NewLastCommentId = FirstCommentId,
            DeletedByUserId = AuthorId,
            DeletedUtc = Created.AddHours(1),
        });

        (await context.Games.FindAsync(EntityId))!.LastCommentId.Should().Be(FirstCommentId);
    }

    /// <inheritdoc cref="EmptyWhenTheBlogLosesItsOnlyComment" />
    [Fact]
    public async Task EmptyWhenTheGameLosesItsOnlyComment()
    {
        using var context = Seeded(AddGame);

        await Games(context).Delete(new DeleteGameCommentEntity
        {
            CommentId = LastCommentId,
            GameId = EntityId,
            NewCommentCount = 0,
            NewLastCommentId = null,
            DeletedByUserId = AuthorId,
            DeletedUtc = Created.AddHours(1),
        });

        (await context.Games.FindAsync(EntityId))!.LastCommentId.Should().BeNull(
            "the pointer named the deleted comment and nothing is left to name");
    }
}
