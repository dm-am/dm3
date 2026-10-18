using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Publications;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Comments;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPublication = DM.Infrastructure.Persistence.Entities.Blog.Publication;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// A denormalised count is what the database made of it, not what a caller read a
/// query earlier and wrote back.
/// </summary>
/// <remarks>
/// CommentCount and PublicationCount are stored columns and nothing recomputes them:
/// the count was read into a service, incremented there and assigned to the owner row,
/// so two writers who read the same number wrote the same number back and one of the
/// two writes was never counted. Deletion had the mirror image — read the comment,
/// check it, mark it — and two removals of one comment both found it live and both
/// took the count down.
///
/// Run against PostgreSQL because both fixes are set-based statements: the increment
/// that also locks the owner row, and the conditional mark whose match count decides
/// whether anything else happens. Neither has an in-memory implementation, and neither
/// is observable at all without two writers running at once.
/// </remarks>
public class DiscussionCountersShould : IntegrationTestBase
{
    private static readonly DateTimeOffset Written = new(2026, 5, 1, 9, 0, 0, TimeSpan.Zero);

    public DiscussionCountersShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task CountBothCommentsWrittenToOneGameAtOnce()
    {
        var gameId = await AddGame();

        var commentIds = await Task.WhenAll(
            CreateComment(gameId, Written),
            CreateComment(gameId, Written.AddMinutes(1)));

        var game = await Reread(g => g.GameId == gameId);
        game.CommentCount.Should().Be(2,
            "both comments are there, and a count that lost one of them has nothing left " +
            "to reconcile itself against");
        game.LastCommentId.Should().Be(commentIds[1], "the pointer names the newest comment");
    }

    /// <summary>
    /// The pointer does not walk backwards when an older comment lands last.
    /// </summary>
    /// <remarks>
    /// CreatedUtc is stamped by the service before the write reaches the owner row, so
    /// the comment that commits last is not necessarily the newest one — a request that
    /// waited on a lock, on a retry or on a slow round trip commits after a comment
    /// written later than it. Assigning the pointer unconditionally then advertised an
    /// older reply as the game's last activity, and the list orders by it.
    ///
    /// Written in order rather than in parallel: which of two concurrent comments commits
    /// last is exactly what a test cannot pin down, and the defect is about the one that
    /// does.
    /// </remarks>
    [Fact]
    public async Task LeaveThePointerOnTheNewerCommentWhenAnOlderOneArrivesAfterIt()
    {
        var gameId = await AddGame();

        var newer = await CreateComment(gameId, Written.AddMinutes(10));
        await CreateComment(gameId, Written);

        var game = await Reread(g => g.GameId == gameId);
        game.CommentCount.Should().Be(2, "both comments were written");
        game.LastCommentId.Should().Be(newer,
            "the pointer is the game's last activity, and the comment that arrived late " +
            "was written earlier than the one it would have replaced");
    }

    [Fact]
    public async Task LowerTheCountOnceWhenOneCommentIsDeletedTwiceAtOnce()
    {
        var gameId = await AddGame();
        var doomed = await CreateComment(gameId, Written);
        await CreateComment(gameId, Written.AddMinutes(1));

        await Task.WhenAll(DeleteComment(gameId, doomed), DeleteComment(gameId, doomed));

        var game = await Reread(g => g.GameId == gameId);
        game.CommentCount.Should().Be(1,
            "one comment was removed, whatever the number of callers that asked for it");
    }

    [Fact]
    public async Task CountBothPublicationsWrittenToOneBlogAtOnce()
    {
        var blogId = await AddBlog();

        await Task.WhenAll(CreatePublication(blogId), CreatePublication(blogId));

        var blog = await RereadBlog(blogId);
        blog.PublicationCount.Should().Be(2,
            "both publications are in the blog, and the number beside its name says how many");
    }

    [Fact]
    public async Task LowerThePublicationCountOnceWhenOnePublicationIsDeletedTwiceAtOnce()
    {
        var blogId = await AddBlog();
        var doomed = await CreatePublication(blogId);
        await CreatePublication(blogId);

        await Task.WhenAll(DeletePublication(doomed), DeletePublication(doomed));

        var blog = await RereadBlog(blogId);
        blog.PublicationCount.Should().Be(1,
            "one publication was removed, whatever the number of callers that asked for it");

        // The mark and the count are one statement now, so the audit the mark carries is
        // asserted with it: moderation has to be able to answer who removed a publication,
        // and a set-based write names the two columns itself or leaves them empty.
        var removed = await RereadPublication(doomed);
        removed.DeletedByUserId.Should().Be(TestConstants.TestUserId);
        removed.DeletedUtc.Should().NotBeNull();
    }

    // One scope per concurrent call: a DbContext is scoped and must not be shared
    // between parallel callers.
    private async Task<Guid> CreateComment(Guid gameId, DateTimeOffset createdUtc)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameCommentRepository>();
        var commentId = Guid.NewGuid();
        await repository.Create(new CreateGameCommentEntity
        {
            CommentId = commentId,
            GameId = gameId,
            AuthorId = TestConstants.TestUserId,
            Text = "Реплика",
            CreatedUtc = createdUtc,
        });
        return commentId;
    }

    private async Task DeleteComment(Guid gameId, Guid commentId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGameCommentRepository>();
        await repository.Delete(new DeleteGameCommentEntity
        {
            CommentId = commentId,
            GameId = gameId,
            DeletedByUserId = TestConstants.TestUserId,
            DeletedUtc = Written.AddHours(1),
        });
    }

    private async Task<Guid> CreatePublication(Guid blogId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPublicationRepository>();
        var publicationId = Guid.NewGuid();
        await repository.CreatePublication(new CreatePublicationEntity
        {
            PublicationId = publicationId,
            BlogId = blogId,
            AuthorId = TestConstants.TestUserId,
            Title = "Запись",
            Content = "text",
            Preview = "text",
            PublishImmediately = true,
            CreatedUtc = Written,
        });
        return publicationId;
    }

    private async Task DeletePublication(Guid publicationId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPublicationRepository>();
        await repository.DeletePublication(publicationId, TestConstants.TestUserId);
    }

    private async Task<Guid> AddGame()
    {
        var gameId = Guid.NewGuid();
        await using var dbContext = DatabaseFixture.CreateDbContext();
        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Title = "Игра для счетчика",
            MasterId = TestConstants.TestUserId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CreatedUtc = Written,
        });
        await dbContext.SaveChangesAsync();
        return gameId;
    }

    private async Task<Guid> AddBlog()
    {
        var blogId = Guid.NewGuid();
        await using var dbContext = DatabaseFixture.CreateDbContext();
        dbContext.Blogs.Add(new DbBlog
        {
            BlogId = blogId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            AuthorId = TestConstants.TestUserId,
            Title = "Блог для счетчика",
            Description = string.Empty,
            CreatedUtc = Written,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });
        await dbContext.SaveChangesAsync();
        return blogId;
    }

    private async Task<DbGame> Reread(System.Linq.Expressions.Expression<Func<DbGame, bool>> predicate)
    {
        await using var dbContext = DatabaseFixture.CreateDbContext();
        return await dbContext.Games.IgnoreQueryFilters().AsNoTracking().SingleAsync(predicate);
    }

    private async Task<DbBlog> RereadBlog(Guid blogId)
    {
        await using var dbContext = DatabaseFixture.CreateDbContext();
        return await dbContext.Blogs.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(b => b.BlogId == blogId);
    }

    private async Task<DbPublication> RereadPublication(Guid publicationId)
    {
        await using var dbContext = DatabaseFixture.CreateDbContext();
        return await dbContext.Publications.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(p => p.PublicationId == publicationId);
    }
}
