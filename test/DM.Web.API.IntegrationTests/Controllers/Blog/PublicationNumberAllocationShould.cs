using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Publications;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;

namespace DM.Web.API.IntegrationTests.Controllers.Blog;

/// <summary>
/// A publication gets its number within the blog when it is created, the way a
/// topic gets its number within the board.
/// </summary>
/// <remarks>
/// Only the seed numbered publications: the creation path never set the column,
/// so every publication written by the site was stored as 0 and a blog's
/// numbers named nothing. The number is allocated as MAX+1, which is only an
/// allocation while the blog row is held, and the unique index on
/// (BlogId, PublicationNumber) is the invariant that holds whoever writes.
/// </remarks>
public class PublicationNumberAllocationShould : IntegrationTestBase
{
    private const int Parallelism = 8;

    public PublicationNumberAllocationShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GiveConcurrentPublicationsOfOneBlogDistinctNumbers()
    {
        var blogId = await CreateBlog();
        try
        {
            var ids = await Task.WhenAll(Enumerable
                .Range(0, Parallelism)
                .Select(i => CreatePublication(blogId, i)));

            (await Numbers(ids)).Should().BeEquivalentTo(Enumerable.Range(1, Parallelism));
        }
        finally
        {
            await DropBlog(blogId);
        }
    }

    /// <summary>
    /// A removed publication keeps its number, so the next one does not take it.
    /// </summary>
    [Fact]
    public async Task NotHandOutTheNumberOfARemovedPublication()
    {
        var blogId = await CreateBlog();
        try
        {
            var removed = await CreatePublication(blogId, 0);

            using (var scope = DatabaseFixture.Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
                await dbContext.Publications
                    .Where(p => p.PublicationId == removed)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsRemoved, true));
            }

            var next = await CreatePublication(blogId, 1);

            (await Numbers([next])).Should().Equal([2]);
        }
        finally
        {
            await DropBlog(blogId);
        }
    }

    [Fact]
    public async Task RejectADuplicateNumberInTheSameBlog()
    {
        var blogId = await CreateBlog();
        try
        {
            await CreatePublication(blogId, 0);

            using var scope = DatabaseFixture.Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
            var insertDuplicate = async () => await dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO "Publications" ("PublicationId", "BlogId", "PublicationNumber", "AuthorId", "Title",
                                            "Content", "Preview", "IsPublished", "CreatedUtc",
                                            "IsRemoved", "ViewCount", "CommentCount")
                VALUES ({0}, {1}, 1, {2}, 'duplicate', '', '', true, now(), false, 0, 0)
                """,
                Guid.NewGuid(), blogId, TestConstants.TestUserId);

            (await insertDuplicate.Should().ThrowAsync<PostgresException>())
                .Which.ConstraintName.Should().Be("IX_Publications_BlogId_PublicationNumber");
        }
        finally
        {
            await DropBlog(blogId);
        }
    }

    private async Task<Guid> CreatePublication(Guid blogId, int index)
    {
        // One scope per concurrent create: a DbContext is scoped and must not be
        // shared between parallel calls.
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPublicationRepository>();
        var publicationId = Guid.NewGuid();
        await repository.CreatePublication(new CreatePublicationEntity
        {
            PublicationId = publicationId,
            BlogId = blogId,
            AuthorId = TestConstants.TestUserId,
            Title = $"Concurrent publication {index}",
            Content = "text",
            PublishImmediately = true,
            CreatedUtc = DateTimeOffset.UtcNow
        });
        return publicationId;
    }

    private async Task<int[]> Numbers(Guid[] publicationIds)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        return await dbContext.Publications
            .IgnoreQueryFilters()
            .Where(p => publicationIds.Contains(p.PublicationId))
            .Select(p => p.PublicationNumber)
            .ToArrayAsync();
    }

    private async Task<Guid> CreateBlog()
    {
        var blogId = Guid.NewGuid();
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        dbContext.Blogs.Add(new DbBlog
        {
            BlogId = blogId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            AuthorId = TestConstants.TestUserId,
            Title = "Publication numbers blog",
            Description = "Seeded by PublicationNumberAllocationShould",
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CreatedUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
        return blogId;
    }

    private async Task DropBlog(Guid blogId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Publications" WHERE "BlogId" = {0}""", blogId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Blogs" WHERE "BlogId" = {0}""", blogId);
    }
}
