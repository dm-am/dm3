using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blacklists;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbBlogBlacklist = DM.Infrastructure.Persistence.Entities.Blog.BlogBlacklist;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// A blog's blacklist is read oldest entry first, and entries made at the same
/// moment are read by name, the order the game's list keeps
/// (GameBlacklistOrderShould).
/// </summary>
/// <remarks>
/// A name just added belongs at the bottom of the list, and a read without an
/// order gave the database the choice. The copy of the owner's personal list
/// writes one moment on every row, so the date alone does not settle the order
/// either.
///
/// The rows are written in the order no read should return them: newest first,
/// and of the two made together the later name first. The blocked users'
/// identifiers rise in insertion order too, so neither the heap, the index on
/// (BlogId, BlockedUserId) nor the users table can hand back the expected order
/// by accident.
/// </remarks>
public class BlogBlacklistOrderShould : IntegrationTestBase
{
    private static readonly DateTimeOffset Earlier = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = new(2026, 3, 2, 12, 0, 0, TimeSpan.Zero);

    public BlogBlacklistOrderShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task ReadTheListByTheMomentOfEntryThenByName()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IBlogBlacklistRepository>();

        var (ownerId, blogId) = await AddBlogAsync(dbContext);

        // One prefix for the three names, so that only the last letter orders them
        // and every collation agrees on that order.
        var prefix = $"bbo{Guid.NewGuid():N}"[..19];
        var userIds = Enumerable.Range(0, 3)
            .Select(_ => Guid.NewGuid())
            .OrderBy(id => id.ToString("N"), StringComparer.Ordinal)
            .ToArray();

        var newest = await AddUserAsync(dbContext, userIds[0], prefix + "a");
        var laterName = await AddUserAsync(dbContext, userIds[1], prefix + "c");
        var earlierName = await AddUserAsync(dbContext, userIds[2], prefix + "b");

        // One save per row, so the heap holds them in exactly this order: a batch
        // would be free to sort its inserts by key.
        await AddEntryAsync(dbContext, blogId, newest, ownerId, Later);
        await AddEntryAsync(dbContext, blogId, laterName, ownerId, Earlier);
        await AddEntryAsync(dbContext, blogId, earlierName, ownerId, Earlier);

        var list = await repository.GetBlacklist(blogId);

        list.Select(u => u.UserId).Should().Equal(
            [earlierName, laterName, newest],
            "the list is read by the moment of entry, and by name where that moment is the same");
    }

    private static async Task AddEntryAsync(
        DmDbContext dbContext, Guid blogId, Guid blockedUserId, Guid blockedByUserId, DateTimeOffset createdUtc)
    {
        dbContext.BlogBlacklists.Add(new DbBlogBlacklist
        {
            EntryId = Guid.NewGuid(),
            BlogId = blogId,
            BlockedUserId = blockedUserId,
            BlockedByUserId = blockedByUserId,
            CreatedUtc = createdUtc
        });
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// A user of this test's own: the fixture database is shared and seeded, so a
    /// fixed identifier would depend on whoever else wrote one.
    /// </summary>
    private static async Task<Guid> AddUserAsync(DmDbContext dbContext, Guid userId, string username)
    {
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            Username = username,
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
        var ownerId = Guid.NewGuid();
        // Username is varchar(20) and uniquely indexed, so the id is truncated
        // rather than used whole.
        await AddUserAsync(dbContext, ownerId, $"bbm{ownerId:N}"[..20]);
        var blogId = Guid.NewGuid();

        dbContext.Blogs.Add(new DbBlog
        {
            BlogId = blogId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            AuthorId = ownerId,
            Title = "Blog for the blacklist order check",
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CreatedUtc = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        return (ownerId, blogId);
    }
}
