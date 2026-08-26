using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// A refusal says only that it refused.
/// </summary>
/// <remarks>
/// Every authorization refusal in the project raises IntentionManagerException,
/// whose message the problem-details factory used to put in the response title —
/// and that message carried JsonSerializer.Serialize(target). So a refused request
/// answered with the whole object it had refused: measured against the running
/// stack before the fix, a read refused on a blog came back 403 carrying the blog
/// DTO, the description and the author's GeneralUser, email and real name
/// included.
///
/// One test rather than one per endpoint, deliberately: the leak was in the
/// exception and in the factory, so every 403 with a target had it and every 403
/// with a target is fixed by the same two edits. What is guarded here is that
/// shape — if somebody puts the message back into the response, this reddens
/// whichever endpoint it happens on.
///
/// The case used to be an anonymous read of a private draft. That read now
/// answers 404, because on a blog the caller cannot see, the difference between
/// "no such blog" and "not yours" is itself a leak. So the subject moved to the
/// nearest refusal that still names a target and must stay a 403: editing a blog
/// the caller can see and does not own.
/// </remarks>
public class RefusalsShould : IntegrationTestBase
{
    public RefusalsShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task NotCarryTheTargetTheyRefusedAccessTo()
    {
        var blog = await AddVisibleBlogAsync();
        var stranger = await AddStrangerAsync();

        var request = CreateAuthenticatedRequest(
            HttpMethod.Patch, $"/v1/blogs/{blog.PublicId}", stranger);
        request.Content = JsonContent.Create(new { title = "Чужая правка" });
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the caller can see this blog and simply may not edit it");

        var body = await response.Content.ReadAsStringAsync();

        // The author's own data first: this is the part that made the leak a
        // personal-data one rather than only a blog one.
        body.Should().NotContain(blog.Email);
        body.Should().NotContain(blog.Username);

        // Then the blog itself.
        body.Should().NotContain(blog.Title);
        body.Should().NotContain(blog.Description);

        // And the shape of the old message, in case a target is described some
        // other way later: a serialized object in a title starts like this.
        body.Should().NotContain("is not allowed to perform");
    }

    private sealed record SeededBlog(
        string PublicId, string Username, string Email, string Title, string Description);

    /// <summary>
    /// A fresh author and a blog anybody may open. Fresh rather than seeded
    /// because the assertions search the response for these exact strings, and a
    /// value another test also uses would make the test lie.
    /// </summary>
    private async Task<SeededBlog> AddVisibleBlogAsync()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var userId = Guid.NewGuid();
        // Username is varchar(20) and uniquely indexed, so the id is truncated.
        var username = $"ref{userId:N}"[..20];
        var email = $"{userId:N}@refusal.example";
        var title = $"Blog title {userId:N}"[..24];
        var description = $"Blog description {userId:N}"[..30];
        var publicId = Guid.NewGuid().ToString("N")[..10];

        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            Username = username,
            Email = email,
            PasswordHash = "hash",
            Salt = "salt",
        });
        dbContext.Blogs.Add(new DbBlog
        {
            BlogId = Guid.NewGuid(),
            PublicId = publicId,
            SerialNumber = 1_000 + dbContext.Blogs.Count(),
            AuthorId = userId,
            Title = title,
            Description = description,
            CreatedUtc = DateTimeOffset.UtcNow,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            DraftVisibility = DraftVisibility.Public,
            CommentsEnabled = true,
            IsRemoved = false,
        });

        await dbContext.SaveChangesAsync();
        return new SeededBlog(publicId, username, email, title, description);
    }

    /// <summary>Somebody with an account and no role in that blog.</summary>
    private async Task<GeneralUser> AddStrangerAsync()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var userId = Guid.NewGuid();
        var username = $"rfs{userId:N}"[..20];
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            Username = username,
            Email = $"{userId:N}@refusalstranger.example",
            PasswordHash = "hash",
            Salt = "salt",
        });
        await dbContext.SaveChangesAsync();

        return new GeneralUser { UserId = userId, Username = username, Role = UserRole.RegularUser };
    }
}
