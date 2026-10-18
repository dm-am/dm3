using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blacklists;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbUserBlacklist = DM.Infrastructure.Persistence.Entities.Account.UserBlacklist;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// One user is on one blog's blacklist once, the rule the game's list already
/// keeps (GameBlacklistUniquenessShould).
/// </summary>
/// <remarks>
/// Adding is check-then-insert: the service reads the list off the blog and
/// inserts a statement later, and two requests for one name both passed that
/// read and both inserted. The blog then showed the name twice. A rule about
/// rows that do not exist yet can only be stated in the schema, so what is
/// asserted here is that the refusal comes from there and that the repository
/// answers it in the domain's own currency.
/// </remarks>
public class BlogBlacklistUniquenessShould : IntegrationTestBase
{
    public BlogBlacklistUniquenessShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task RefuseASecondEntryForOneUserOnOneBlog()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IBlogBlacklistRepository>();

        var (ownerId, blogId) = await AddBlogAsync(dbContext);
        var blockedUserId = await AddUserAsync(dbContext);

        await repository.Add(blogId, blockedUserId, ownerId);

        // Another entry identifier, the same pair: the insert the loser of the race
        // issues after its own check came back empty.
        Func<Task> second = () => repository.Add(blogId, blockedUserId, ownerId);

        var refusal = await second.Should().ThrowAsync<DuplicateEntityException>(
            "one user is on one blog's list once, whatever identifier the row carries");
        refusal.And.InnerException.Should().BeOfType<DbUpdateException>()
            .Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be("23505",
                "the refusal is the index answering the insert, not a read answering it");

        (await dbContext.BlogBlacklists.CountAsync(
                b => b.BlogId == blogId && b.BlockedUserId == blockedUserId))
            .Should().Be(1);
    }

    /// <summary>
    /// The owner's personal list lands on the blog once, however many copies of it
    /// run at the same time.
    /// </summary>
    /// <remarks>
    /// The copy skipped what it saw on the list already, and a read cannot see the
    /// copy beside it: with the index in place, the second of two copies would have
    /// been refused outright with a 23505. The statement now settles every pair
    /// against the index itself, so each copy ends without an error and between
    /// them they write every entry exactly once.
    /// </remarks>
    [Fact]
    public async Task CopyThePersonalListOnceWhenCopiesRunTogether()
    {
        const int entries = 20;
        const int copies = 8;

        Guid ownerId;
        Guid blogId;
        using (var scope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
            (ownerId, blogId) = await AddBlogAsync(dbContext);
            for (var i = 0; i < entries; i++)
            {
                dbContext.UserBlacklists.Add(new DbUserBlacklist
                {
                    EntryId = Guid.NewGuid(),
                    OwnerId = ownerId,
                    BlockedUserId = await AddUserAsync(dbContext),
                    CreatedUtc = DateTimeOffset.UtcNow
                });
            }

            await dbContext.SaveChangesAsync();
        }

        var written = await Task.WhenAll(Enumerable.Range(0, copies).Select(async _ =>
        {
            // One scope per copy: a DbContext is scoped and must not be shared
            // between parallel calls.
            using var scope = DatabaseFixture.Factory.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBlogBlacklistRepository>();
            return await repository.CopyFromPersonalBlacklist(blogId, ownerId);
        }));

        written.Sum().Should().Be(entries, "each entry is written by exactly one of the copies");

        using (var scope = DatabaseFixture.Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
            (await dbContext.BlogBlacklists.CountAsync(b => b.BlogId == blogId)).Should().Be(entries);
        }
    }

    /// <summary>
    /// A user of this test's own: the fixture database is shared and seeded, so a
    /// fixed identifier would depend on whoever else wrote one.
    /// </summary>
    private static async Task<Guid> AddUserAsync(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id is
            // truncated rather than used whole.
            Username = $"bbl{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        await dbContext.SaveChangesAsync();
        return userId;
    }

    /// <summary>A blog with the owner it needs to exist.</summary>
    private static async Task<(Guid OwnerId, Guid BlogId)> AddBlogAsync(DmDbContext dbContext)
    {
        var ownerId = await AddUserAsync(dbContext);
        var blogId = Guid.NewGuid();

        dbContext.Blogs.Add(new DbBlog
        {
            BlogId = blogId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            AuthorId = ownerId,
            Title = "Blog for the blacklist uniqueness check",
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CreatedUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        return (ownerId, blogId);
    }
}
