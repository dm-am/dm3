using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbRubric = DM.Infrastructure.Persistence.Entities.Blog.Rubric;
using DbSubscription = DM.Infrastructure.Persistence.Entities.Subscriptions.Subscription;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Runs against the container Postgres, not the InMemory provider, because the
/// two questions this asks are relational ones: the public id is encoded from a
/// serial number the database generates, and the rubric order is a batch of row
/// updates. InMemory generated no serial, so the encode had to be stubbed —
/// leaving the test asserting that a mock returned what the mock was told to.
/// </summary>
public class BlogRepositoryShould : IntegrationTestBase
{
    public BlogRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task EncodePublicIdFromTheSerialNumberTheDatabaseAssigns()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBlogRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var authorId = await AddAuthorAsync(dbContext);
        var blogId = Guid.NewGuid();

        var blog = await repository.CreateBlog(new CreateBlogEntity
        {
            BlogId = blogId,
            OwnerId = authorId,
            Title = "Blog for the serial number check",
            DraftVisibility = DraftVisibility.Public,
            CommentsEnabled = true,
            CreatedUtc = DateTimeOffset.UtcNow,
        });

        var stored = await dbContext.Blogs.AsNoTracking().FirstAsync(b => b.BlogId == blogId);

        // The serial is the database's, so this is the whole chain: identity
        // column -> encode -> persisted PublicId -> the value the DTO carries.
        stored.SerialNumber.Should().BeGreaterThan(0);
        stored.PublicId.Should().NotBeNullOrEmpty();
        blog.PublicId.Should().Be(stored.PublicId);

        // Every seeded blog holds one too, and no two agree — the encoding is
        // what the alias URLs are addressed by.
        var publicIds = await dbContext.Blogs.AsNoTracking()
            .Select(b => b.PublicId).ToListAsync();
        publicIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task AssignSortOrderByPositionWhenReordering()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBlogRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var authorId = await AddAuthorAsync(dbContext);
        var blogId = Guid.NewGuid();
        await repository.CreateBlog(new CreateBlogEntity
        {
            BlogId = blogId,
            OwnerId = authorId,
            Title = "Blog for the rubric order check",
            DraftVisibility = DraftVisibility.Public,
            CommentsEnabled = true,
            CreatedUtc = DateTimeOffset.UtcNow,
        });

        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        dbContext.Rubrics.AddRange(
            new DbRubric { RubricId = first, BlogId = blogId, Title = "A", SortOrder = 0 },
            new DbRubric { RubricId = second, BlogId = blogId, Title = "B", SortOrder = 1 },
            new DbRubric { RubricId = third, BlogId = blogId, Title = "C", SortOrder = 2 });
        await dbContext.SaveChangesAsync();

        await repository.ReorderRubrics(blogId, new[] { third, second, first });

        var order = await dbContext.Rubrics.AsNoTracking()
            .Where(r => r.BlogId == blogId)
            .OrderBy(r => r.SortOrder)
            .Select(r => r.RubricId)
            .ToListAsync();

        order.Should().Equal(third, second, first);
    }

    /// <summary>
    /// A single-blog read knows whether the caller is subscribed to it.
    /// </summary>
    /// <remarks>
    /// IsViewerSubscriber is the reader role, and the ViewDraft rule is decided on
    /// exactly this read. Filled by the list-shaped reads alone it was false for
    /// everybody here, so a reader of a private-draft blog was indistinguishable
    /// from a stranger and could not open the blog they had been invited to. The
    /// game side fills the same field on its own single-game read.
    /// </remarks>
    [Fact]
    public async Task KnowTheViewerIsSubscribedOnASingleBlogRead()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBlogRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var authorId = await AddAuthorAsync(dbContext);
        var readerId = await AddAuthorAsync(dbContext);
        var blogId = Guid.NewGuid();

        await repository.CreateBlog(new CreateBlogEntity
        {
            BlogId = blogId,
            OwnerId = authorId,
            Title = "Blog with an invited reader",
            DraftVisibility = DraftVisibility.Private,
            CommentsEnabled = true,
            CreatedUtc = DateTimeOffset.UtcNow,
        });

        dbContext.Subscriptions.Add(new DbSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            SubscriberId = readerId,
            TargetType = SubscriptionTargetType.Blog,
            TargetId = blogId,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        var forTheReader = await repository.Get(blogId, readerId);
        var forAStranger = await repository.Get(blogId, Guid.NewGuid());
        var byAddress = await repository.GetByPublicId(forTheReader!.PublicId, readerId);

        forTheReader.IsViewerSubscriber.Should().BeTrue();
        forAStranger!.IsViewerSubscriber.Should().BeFalse();
        // Both addresses of the same blog answer the authorization question the
        // same way: the alias URL is the one a shared invitation link carries.
        byAddress!.IsViewerSubscriber.Should().BeTrue();
    }

    /// <summary>
    /// A fresh author per test: the fixture's database is shared, so a fixed id
    /// would make two tests fight over the same row.
    /// </summary>
    private static async Task<Guid> AddAuthorAsync(DmDbContext dbContext)
    {
        var authorId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = authorId,
            // Username is varchar(20) and uniquely indexed, so the id is
            // truncated rather than used whole.
            Username = $"blog{authorId:N}"[..20],
            Email = $"{authorId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        await dbContext.SaveChangesAsync();
        return authorId;
    }
}
