using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;

namespace DM.Web.API.IntegrationTests.Controllers.Blog;

public class BlogDescriptionShould : IntegrationTestBase
{
    public BlogDescriptionShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task KeepTheDescriptionAnEditLeavesOutAndClearAnEmptyOne()
    {
        var publicId = await SeedBlog("[B]О блоге[/B]");

        try
        {
            (await Patch(publicId, new { title = "Блог", draftVisibility = "Public" })).Should().Be(HttpStatusCode.OK);
            (await Description(publicId)).Should().Be("[B]О блоге[/B]", "an edit without the field keeps it");

            (await Patch(publicId, new { title = "Блог", description = "  [I]новое[/I]\n" })).Should().Be(HttpStatusCode.OK);
            (await Description(publicId)).Should().Be("  [I]новое[/I]\n", "the description is stored as sent");

            (await Patch(publicId, new { title = "Блог", description = "" })).Should().Be(HttpStatusCode.OK);
            (await Description(publicId)).Should().BeEmpty("an empty description clears it");
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            await db.Set<DbBlog>().Where(b => b.PublicId == publicId).ExecuteDeleteAsync();
        }
    }

    private async Task<HttpStatusCode> Patch(string publicId, object body)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Patch, $"/v1/blogs/{publicId}");
        request.Content = JsonContent.Create(body);
        return (await Client.SendAsync(request)).StatusCode;
    }

    private async Task<string?> Description(string publicId)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/blogs/{publicId}");
        request.Headers.Add(BbAudienceHeader.HeaderName, "author_edit");
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("resource").GetProperty("description").GetString();
    }

    private async Task<string> SeedBlog(string description)
    {
        var publicId = Guid.NewGuid().ToString("N")[..10];
        await using var db = DatabaseFixture.CreateDbContext();
        db.Set<DbBlog>().Add(new DbBlog
        {
            BlogId = Guid.NewGuid(),
            PublicId = publicId,
            AuthorId = TestConstants.TestUserId,
            Title = "Блог",
            Description = description,
            CreatedUtc = DateTimeOffset.UtcNow,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            DraftVisibility = DraftVisibility.Public,
        });
        await db.SaveChangesAsync();
        return publicId;
    }
}
