using System;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Enums;
using DM.Domain.Forum.Features.Comments;
using DM.Domain.Game.Features.Comments;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbBoard = DM.Infrastructure.Persistence.Entities.Forum.Board;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;
using DbTopic = DM.Infrastructure.Persistence.Entities.Forum.Topic;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Deleting a comment from the middle of a discussion leaves the last-comment
/// pointer where it is, and takes the count down by one.
/// </summary>
/// <remarks>
/// Which comment the pointer moves to is decided inside the transaction that marks
/// the comment removed, and the write is guarded by the pointer still naming that
/// comment. Writing it unconditionally erased the pointer whenever somebody deleted
/// an earlier comment, and the reading side takes the discussion's last activity
/// from that pointer.
///
/// All four discussions are asserted here rather than on the in-memory provider three
/// of them used to share with the forum: a deletion is now set-based statements — the
/// conditional mark, the conditional pointer, and for the three that keep one, the
/// floored decrement — and neither ExecuteUpdate nor a row lock has an in-memory
/// implementation, so the same cases asserted there would have asserted nothing.
///
/// The topic is the one owner of the four that keeps no comment count: a topic carries
/// the pointer and nothing else, so its cases assert the pointer alone.
/// </remarks>
public class LastCommentPointerShould : IntegrationTestBase
{
    private static readonly DateTimeOffset Created = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

    public LastCommentPointerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task StayOnTheBlogWhenAnEarlierCommentGoes()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IBlogCommentRepository>();

        var (blogId, first, last) = await SeedBlog(dbContext);

        await repository.Delete(Deletion(first, blogId));

        var blog = await Reread(dbContext.Blogs, b => b.BlogId == blogId);
        blog.LastCommentId.Should().Be(last,
            "the deleted comment is not the one the pointer names");
        blog.CommentCount.Should().Be(1, "one of the two comments is gone");
    }

    [Fact]
    public async Task MoveOnTheBlogWhenItsLastCommentGoes()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IBlogCommentRepository>();

        var (blogId, first, last) = await SeedBlog(dbContext);

        await repository.Delete(Deletion(last, blogId));

        var blog = await Reread(dbContext.Blogs, b => b.BlogId == blogId);
        blog.LastCommentId.Should().Be(first,
            "the pointer names the newest comment left, and that is the second last one");
        blog.CommentCount.Should().Be(1);
    }

    /// <summary>
    /// The guard has two halves and this is the second one.
    /// </summary>
    /// <remarks>
    /// "Move it to the newest comment left" is one half; "clear it when there is no
    /// comment left to name" is the other, and only the second answers the last comment
    /// of a discussion. A guard cut down to the first half passes every other test in
    /// this class while leaving the pointer on a soft-deleted row, which the global
    /// filter then reads as no comment at all, one query later.
    /// </remarks>
    [Fact]
    public async Task EmptyWhenTheBlogLosesItsOnlyComment()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IBlogCommentRepository>();

        var blogId = await AddBlog(dbContext);
        var only = await AddOnlyComment(dbContext, blogId);
        await PointAt(dbContext.Blogs, b => b.BlogId == blogId,
            b => b.CommentCount, b => b.LastCommentId, only, commentCount: 1);

        await repository.Delete(Deletion(only, blogId));

        var blog = await Reread(dbContext.Blogs, b => b.BlogId == blogId);
        blog.LastCommentId.Should().BeNull(
            "the pointer named the deleted comment and no comment is left to take it over; " +
            "leaving it there points the blog at a row every read filters out");
    }

    [Fact]
    public async Task StayOnThePublicationWhenAnEarlierCommentGoes()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IPublicationCommentRepository>();

        var (publicationId, first, last) = await SeedPublication(dbContext);

        await repository.Delete(new DeletePublicationCommentEntity
        {
            CommentId = first,
            PublicationId = publicationId,
            DeletedByUserId = TestConstants.TestUserId,
            DeletedUtc = Created.AddHours(1),
        });

        var publication = await Reread(dbContext.Publications, p => p.PublicationId == publicationId);
        publication.LastCommentId.Should().Be(last,
            "the deleted comment is not the one the pointer names");
        publication.CommentCount.Should().Be(1, "one of the two comments is gone");
    }

    [Fact]
    public async Task MoveOnThePublicationWhenItsLastCommentGoes()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IPublicationCommentRepository>();

        var (publicationId, first, last) = await SeedPublication(dbContext);

        await repository.Delete(new DeletePublicationCommentEntity
        {
            CommentId = last,
            PublicationId = publicationId,
            DeletedByUserId = TestConstants.TestUserId,
            DeletedUtc = Created.AddHours(1),
        });

        var publication = await Reread(dbContext.Publications, p => p.PublicationId == publicationId);
        publication.LastCommentId.Should().Be(first);
        publication.CommentCount.Should().Be(1);
    }

    /// <inheritdoc cref="EmptyWhenTheBlogLosesItsOnlyComment" />
    [Fact]
    public async Task EmptyWhenThePublicationLosesItsOnlyComment()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IPublicationCommentRepository>();

        var publicationId = await AddPublication(dbContext);
        var only = await AddOnlyComment(dbContext, publicationId);
        await PointAt(dbContext.Publications, p => p.PublicationId == publicationId,
            p => p.CommentCount, p => p.LastCommentId, only, commentCount: 1);

        await repository.Delete(new DeletePublicationCommentEntity
        {
            CommentId = only,
            PublicationId = publicationId,
            DeletedByUserId = TestConstants.TestUserId,
            DeletedUtc = Created.AddHours(1),
        });

        var publication = await Reread(dbContext.Publications, p => p.PublicationId == publicationId);
        publication.LastCommentId.Should().BeNull(
            "the pointer named the deleted comment and no comment is left to take it over");
    }

    [Fact]
    public async Task StayOnTheGameWhenAnEarlierCommentGoes()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IGameCommentRepository>();

        var (gameId, first, last) = await SeedGame(dbContext);

        await repository.Delete(new DeleteGameCommentEntity
        {
            CommentId = first,
            GameId = gameId,
            DeletedByUserId = TestConstants.TestUserId,
            DeletedUtc = Created.AddHours(1),
        });

        var game = await Reread(dbContext.Games, g => g.GameId == gameId);
        game.LastCommentId.Should().Be(last, "the deleted comment is not the one the pointer names");
        game.CommentCount.Should().Be(1, "one of the two comments is gone");
    }

    [Fact]
    public async Task MoveOnTheGameWhenItsLastCommentGoes()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IGameCommentRepository>();

        var (gameId, first, last) = await SeedGame(dbContext);

        await repository.Delete(new DeleteGameCommentEntity
        {
            CommentId = last,
            GameId = gameId,
            DeletedByUserId = TestConstants.TestUserId,
            DeletedUtc = Created.AddHours(1),
        });

        var game = await Reread(dbContext.Games, g => g.GameId == gameId);
        game.LastCommentId.Should().Be(first);
        game.CommentCount.Should().Be(1);
    }

    /// <inheritdoc cref="EmptyWhenTheBlogLosesItsOnlyComment" />
    [Fact]
    public async Task EmptyWhenTheGameLosesItsOnlyComment()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IGameCommentRepository>();

        var gameId = await AddGame(dbContext);
        var only = await AddOnlyComment(dbContext, gameId);
        await PointAt(dbContext.Games, g => g.GameId == gameId,
            g => g.CommentCount, g => g.LastCommentId, only, commentCount: 1);

        await repository.Delete(new DeleteGameCommentEntity
        {
            CommentId = only,
            GameId = gameId,
            DeletedByUserId = TestConstants.TestUserId,
            DeletedUtc = Created.AddHours(1),
        });

        var game = await Reread(dbContext.Games, g => g.GameId == gameId);
        game.LastCommentId.Should().BeNull(
            "the pointer named the deleted comment and no comment is left to take it over");
    }

    [Fact]
    public async Task StayOnTheTopicWhenAnEarlierCommentGoes()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicCommentRepository>();

        var (topicId, first, last) = await SeedTopic(dbContext);

        (await repository.Delete(TopicDeletion(first, topicId)))
            .Should().BeTrue("the comment was there and this call is what removed it");

        var topic = await Reread(dbContext.Topics, t => t.TopicId == topicId);
        topic.LastCommentId.Should().Be(last, "the deleted comment is not the one the pointer names");
    }

    [Fact]
    public async Task MoveOnTheTopicWhenItsLastCommentGoes()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicCommentRepository>();

        var (topicId, first, last) = await SeedTopic(dbContext);

        await repository.Delete(TopicDeletion(last, topicId));

        var topic = await Reread(dbContext.Topics, t => t.TopicId == topicId);
        topic.LastCommentId.Should().Be(first,
            "the pointer names the newest comment left, and that is the second last one");
    }

    /// <inheritdoc cref="EmptyWhenTheBlogLosesItsOnlyComment" />
    [Fact]
    public async Task EmptyWhenTheTopicLosesItsOnlyComment()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicCommentRepository>();

        var topicId = await AddTopic(dbContext);
        var only = await AddOnlyComment(dbContext, topicId);
        await PointTopicAt(dbContext, topicId, only);

        await repository.Delete(TopicDeletion(only, topicId));

        var topic = await Reread(dbContext.Topics, t => t.TopicId == topicId);
        topic.LastCommentId.Should().BeNull(
            "the pointer named the deleted comment and no comment is left to take it over");
    }

    /// <summary>
    /// The second removal of one comment answers that it removed nothing.
    /// </summary>
    /// <remarks>
    /// The answer is what the service gates the topic's unread counter and the
    /// DeletedTopicComment event on. Two removals of one comment - a double click, a
    /// moderator and an author at once - both used to lower the counter and both used
    /// to announce, and the counter has no way back afterwards.
    /// </remarks>
    [Fact]
    public async Task AnswerOnlyOnceWhenOneTopicCommentIsRemovedTwice()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicCommentRepository>();

        var (topicId, _, last) = await SeedTopic(dbContext);

        (await repository.Delete(TopicDeletion(last, topicId))).Should().BeTrue();
        (await repository.Delete(TopicDeletion(last, topicId))).Should().BeFalse(
            "the mark is a conditional update and only one caller writes it");
    }

    /// <summary>
    /// The first comment of a topic takes a pointer that names nothing.
    /// </summary>
    /// <remarks>
    /// The other three owners are also asserted against a pointer left over from a
    /// comment that is no longer in the table; a topic cannot reach that state,
    /// FK_Topics_Comments_LastCommentId standing behind its column, so the null is
    /// the whole of "names nothing" here.
    /// </remarks>
    [Fact]
    public async Task MoveTheTopicOntoItsFirstComment()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicCommentRepository>();

        var topicId = await AddTopic(dbContext);

        var created = await repository.Create(new CreateTopicCommentEntity
        {
            TopicId = topicId,
            AuthorId = TestConstants.TestUserId,
            Text = "Реплика",
        });

        var topic = await Reread(dbContext.Topics, t => t.TopicId == topicId);
        topic.LastCommentId.Should().Be(created.Id,
            "the topic named no comment, so there is nothing the first one has to give way to");
    }

    /// <summary>
    /// A comment written after a newer one does not take the topic's pointer.
    /// </summary>
    /// <remarks>
    /// The pointer used to be assigned on the tracked topic row by every reply, and
    /// the forum list orders topics by it: a reply that waited on a lock or a retry
    /// sent a live discussion backwards in that order, behind topics nobody had
    /// touched since.
    /// </remarks>
    [Fact]
    public async Task LeaveTheTopicPointerOnTheNewerCommentWhenAnOlderOneArrivesAfterIt()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<ITopicCommentRepository>();

        var topicId = await AddTopic(dbContext);
        // Dated past the clock of the host, which is what the repository stamps the
        // comment below with. A discussion holds comments it did not date itself: the
        // DM2 import carries its own timestamps, and two hosts do not agree on the
        // second to begin with.
        var newer = await AddCommentAt(dbContext, topicId, DateTimeOffset.UtcNow.AddHours(1));
        await PointTopicAt(dbContext, topicId, newer);

        await repository.Create(new CreateTopicCommentEntity
        {
            TopicId = topicId,
            AuthorId = TestConstants.TestUserId,
            Text = "Реплика",
        });

        var topic = await Reread(dbContext.Topics, t => t.TopicId == topicId);
        topic.LastCommentId.Should().Be(newer,
            "the comment that arrived late was written earlier than the one on the pointer, " +
            "and the topic list orders by it");
    }

    /// <summary>
    /// A pointer left over from a comment that is no longer in the table gives way to
    /// a comment that is.
    /// </summary>
    /// <remarks>
    /// Nothing stops a blog, a game or a publication from naming a comment that is not
    /// there: no foreign key stands behind the column, and a purge, an import or a
    /// hand-written fix leaves the pointer behind. Stated as a case of its own because
    /// the condition the pointer moves under is a comparison against the row it names,
    /// and a comparison against a row that does not exist answers neither yes nor no.
    /// The blog would keep advertising an activity nobody can open, and the comment
    /// just written would never show as its last.
    /// </remarks>
    [Fact]
    public async Task MoveOffAPointerNamingACommentThatIsNotThere()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IBlogCommentRepository>();

        var blogId = await AddBlog(dbContext);
        await PointAt(dbContext.Blogs, b => b.BlogId == blogId,
            b => b.CommentCount, b => b.LastCommentId, Guid.NewGuid(), commentCount: 0);

        var (_, commentId) = await repository.Create(
            new CreateComment { Text = "Реплика" }, TestConstants.TestUserId, blogId);

        var blog = await Reread(dbContext.Blogs, b => b.BlogId == blogId);
        blog.LastCommentId.Should().Be(commentId,
            "the comment the pointer named is gone, so it names nothing the new comment " +
            "has to give way to");
        blog.CommentCount.Should().Be(1);
    }

    private static DeleteBlogCommentEntity Deletion(Guid commentId, Guid blogId) => new()
    {
        CommentId = commentId,
        BlogId = blogId,
        DeletedByUserId = TestConstants.TestUserId,
        DeletedUtc = Created.AddHours(1),
    };

    /// <summary>
    /// Two comments, the pointer on the newer one — the state every discussion is in.
    /// </summary>
    private static async Task<(Guid First, Guid Last)> AddCommentPair(DmDbContext dbContext, Guid entityId)
    {
        var first = Guid.NewGuid();
        var last = Guid.NewGuid();
        dbContext.Comments.AddRange(
            NewComment(first, entityId, 0),
            NewComment(last, entityId, 10));
        await dbContext.SaveChangesAsync();
        return (first, last);
    }

    private static DbComment NewComment(Guid commentId, Guid entityId, int minutesAfter) => new()
    {
        CommentId = commentId,
        EntityId = entityId,
        AuthorId = TestConstants.TestUserId,
        Text = "Реплика",
        CreatedUtc = Created.AddMinutes(minutesAfter),
        IsRemoved = false,
    };

    private static async Task<(Guid BlogId, Guid First, Guid Last)> SeedBlog(DmDbContext dbContext)
    {
        var blogId = await AddBlog(dbContext);
        var (first, last) = await AddCommentPair(dbContext, blogId);
        await PointAt(dbContext.Blogs, b => b.BlogId == blogId,
            b => b.CommentCount, b => b.LastCommentId, last);
        return (blogId, first, last);
    }

    /// <summary>
    /// One comment, the pointer on it: a discussion with nothing to fall back on.
    /// </summary>
    private static async Task<Guid> AddOnlyComment(DmDbContext dbContext, Guid entityId)
    {
        var commentId = Guid.NewGuid();
        dbContext.Comments.Add(NewComment(commentId, entityId, 0));
        await dbContext.SaveChangesAsync();
        return commentId;
    }

    /// <summary>One comment of the discussion, dated by the caller.</summary>
    private static async Task<Guid> AddCommentAt(
        DmDbContext dbContext, Guid entityId, DateTimeOffset createdUtc)
    {
        var commentId = Guid.NewGuid();
        var comment = NewComment(commentId, entityId, 0);
        comment.CreatedUtc = createdUtc;
        dbContext.Comments.Add(comment);
        await dbContext.SaveChangesAsync();
        return commentId;
    }

    private static async Task<(Guid PublicationId, Guid First, Guid Last)> SeedPublication(
        DmDbContext dbContext)
    {
        var publicationId = await AddPublication(dbContext);
        var (first, last) = await AddCommentPair(dbContext, publicationId);
        await PointAt(dbContext.Publications, p => p.PublicationId == publicationId,
            p => p.CommentCount, p => p.LastCommentId, last);
        return (publicationId, first, last);
    }

    private static async Task<Guid> AddPublication(DmDbContext dbContext)
    {
        var blogId = await AddBlog(dbContext);
        var publicationId = Guid.NewGuid();
        dbContext.Publications.Add(new DbPublication
        {
            PublicationId = publicationId,
            BlogId = blogId,
            PublicationNumber = 1,
            AuthorId = TestConstants.TestUserId,
            Title = "Запись",
            Content = "text",
            Preview = "text",
            IsPublished = true,
            CreatedUtc = Created,
        });
        await dbContext.SaveChangesAsync();
        return publicationId;
    }

    private static DeleteTopicCommentEntity TopicDeletion(Guid commentId, Guid topicId) => new()
    {
        CommentId = commentId,
        TopicId = topicId,
        DeletedByUserId = TestConstants.TestUserId,
        DeletedUtc = Created.AddHours(1),
    };

    private static async Task<(Guid TopicId, Guid First, Guid Last)> SeedTopic(DmDbContext dbContext)
    {
        var topicId = await AddTopic(dbContext);
        var (first, last) = await AddCommentPair(dbContext, topicId);
        await PointTopicAt(dbContext, topicId, last);
        return (topicId, first, last);
    }

    /// <summary>
    /// A topic on a board of its own: the fixture database is shared and seeded, so a
    /// board every test wrote to would be a board every test's count depends on.
    /// </summary>
    private static async Task<Guid> AddTopic(DmDbContext dbContext)
    {
        var boardId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        dbContext.Boards.Add(new DbBoard
        {
            BoardId = boardId,
            Title = $"Раздел для указателя {boardId:N}",
            Alias = $"pointer-{boardId:N}",
            Order = 0,
            ViewPolicy = BoardAccessPolicy.Guest,
            CreateTopicPolicy = BoardAccessPolicy.Guest,
        });
        dbContext.Topics.Add(new DbTopic
        {
            TopicId = topicId,
            BoardId = boardId,
            TopicNumber = 1,
            AuthorId = TestConstants.TestUserId,
            Title = "Тема для указателя",
            Text = string.Empty,
            CreatedUtc = Created,
        });
        await dbContext.SaveChangesAsync();
        return topicId;
    }

    /// <summary>
    /// The topic's starting state: the pointer on the comment given. Written
    /// set-based for the same reason <see cref="PointAt{TEntity}" /> is.
    /// </summary>
    private static Task PointTopicAt(DmDbContext dbContext, Guid topicId, Guid lastCommentId) =>
        dbContext.Topics
            .Where(t => t.TopicId == topicId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastCommentId, (Guid?)lastCommentId));

    private static async Task<(Guid GameId, Guid First, Guid Last)> SeedGame(DmDbContext dbContext)
    {
        var gameId = await AddGame(dbContext);
        var (first, last) = await AddCommentPair(dbContext, gameId);
        await PointAt(dbContext.Games, g => g.GameId == gameId,
            g => g.CommentCount, g => g.LastCommentId, last);
        return (gameId, first, last);
    }

    private static async Task<Guid> AddGame(DmDbContext dbContext)
    {
        var gameId = Guid.NewGuid();
        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Title = "Игра для указателя",
            MasterId = TestConstants.TestUserId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CreatedUtc = Created,
        });
        await dbContext.SaveChangesAsync();
        return gameId;
    }

    private static async Task<Guid> AddBlog(DmDbContext dbContext)
    {
        var blogId = Guid.NewGuid();
        dbContext.Blogs.Add(new DbBlog
        {
            BlogId = blogId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            AuthorId = TestConstants.TestUserId,
            Title = "Блог для указателя",
            Description = string.Empty,
            CreatedUtc = Created,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });
        await dbContext.SaveChangesAsync();
        return blogId;
    }

    /// <summary>
    /// The starting state of the owner row: as many comments counted as it holds, and
    /// the pointer on the newest one. Written set-based so the change tracker holds
    /// nothing the repository under test would then read out of memory instead of out
    /// of the database.
    /// </summary>
    private static Task PointAt<TEntity>(
        DbSet<TEntity> set,
        System.Linq.Expressions.Expression<Func<TEntity, bool>> owner,
        System.Linq.Expressions.Expression<Func<TEntity, int>> count,
        System.Linq.Expressions.Expression<Func<TEntity, Guid?>> pointer,
        Guid lastCommentId,
        int commentCount = 2)
        where TEntity : class =>
        set.Where(owner).ExecuteUpdateAsync(s => s
            .SetProperty(count, commentCount)
            .SetProperty(pointer, (Guid?)lastCommentId));

    private static async Task<TEntity> Reread<TEntity>(
        DbSet<TEntity> set, System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate)
        where TEntity : class =>
        await set.IgnoreQueryFilters().AsNoTracking().SingleAsync(predicate);
}
