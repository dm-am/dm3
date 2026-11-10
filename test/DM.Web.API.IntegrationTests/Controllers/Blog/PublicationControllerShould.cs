using System.Net;
using DM.Domain.Core.Extensions;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Blog;

public class PublicationControllerShould : IntegrationTestBase
{
    public PublicationControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetPublicationsWithUnknownBlogReturnsNotFound()
    {
        // A blog id that is guaranteed not to exist
        var blogId = Guid.NewGuid();

        var response = await Client.GetAsync($"/v1/blogs/{blogId}/publications");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // A notification names a publication by transliterated title, tilde and base64 of the guid;
    // both spellings reach the handler and are refused by it.
    [Fact]
    public async Task GetPublicationAcceptsTheReadableGuidANotificationCarries()
    {
        var missing = Guid.NewGuid();

        var byGuid = await Client.GetAsync($"/v1/publications/{missing}");
        var byReadable = await Client.GetAsync(
            $"/v1/publications/{missing.EncodeToReadable("Полет над гнездом")}");

        byGuid.StatusCode.Should().Be(HttpStatusCode.NotFound);
        byReadable.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "the readable form is an address of the same publication");

        // The handler's refusal, not the router's silence: the body is what tells
        // the two 404s apart.
        var refusal = await byGuid.Content.ReadAsStringAsync();
        refusal.Should().Contain("Публикация не найдена");
        (await byReadable.Content.ReadAsStringAsync()).Should().Contain("Публикация не найдена");
    }
}
