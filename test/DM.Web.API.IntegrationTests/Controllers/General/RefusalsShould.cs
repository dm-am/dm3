using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using FluentAssertions;
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
/// answered with the whole object it had refused. Measured against the running
/// stack before the fix: an anonymous GET of a blog whose draft is private came
/// back 403 carrying the blog DTO, the draft description and the author's
/// GeneralUser, email and real name included.
///
/// One test rather than one per endpoint, deliberately: the leak was in the
/// exception and in the factory, so every 403 with a target had it and every 403
/// with a target is fixed by the same two edits. What is guarded here is that
/// shape — if somebody puts the message back into the response, this reddens
/// whichever endpoint it happens on.
/// </remarks>
public class RefusalsShould : IntegrationTestBase
{
    public RefusalsShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task NotCarryTheTargetTheyRefusedAccessTo()
    {
        var (username, email, title, description) = await AddBlogWithPrivateDraftAsync();

        var response = await Client.GetAsync($"/v1/blogs/owner/{username}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a private draft is not readable by an anonymous caller");

        var body = await response.Content.ReadAsStringAsync();

        // The author's own data first: this is the part that made the leak a
        // personal-data one rather than only a draft one.
        body.Should().NotContain(email);
        body.Should().NotContain(username);

        // Then the draft itself.
        body.Should().NotContain(title);
        body.Should().NotContain(description);

        // And the shape of the old message, in case a target is described some
        // other way later: a serialized object in a title starts like this.
        body.Should().NotContain("is not allowed to perform");
    }

    /// <summary>
    /// A fresh author and a blog whose draft only its owner may read. Fresh rather
    /// than seeded because the assertions search the response for these exact
    /// strings, and a value another test also uses would make the test lie.
    /// </summary>
    private async Task<(string Username, string Email, string Title, string Description)>
        AddBlogWithPrivateDraftAsync()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var userId = Guid.NewGuid();
        // Username is varchar(20) and uniquely indexed, so the id is truncated.
        var username = $"ref{userId:N}"[..20];
        var email = $"{userId:N}@refusal.example";
        var title = $"Draft title {userId:N}"[..24];
        var description = $"Draft description {userId:N}"[..30];

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
            PublicId = Guid.NewGuid().ToString("N")[..10],
            SerialNumber = 1_000 + dbContext.Blogs.Count(),
            AuthorId = userId,
            Title = title,
            Description = description,
            CreatedUtc = DateTimeOffset.UtcNow,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            DraftVisibility = DraftVisibility.Private,
            CommentsEnabled = true,
            IsRemoved = false,
        });

        await dbContext.SaveChangesAsync();
        return (username, email, title, description);
    }
}
