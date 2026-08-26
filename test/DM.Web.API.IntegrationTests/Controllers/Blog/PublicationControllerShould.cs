using System.Net;
using System.Net.Http.Json;
using DM.Domain.Core.Extensions;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Blog;

/// <summary>
/// Integration tests for PublicationController
/// </summary>
public class PublicationControllerShould : IntegrationTestBase
{
    public PublicationControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetPublications_WithUnknownBlog_ReturnsNotFound()
    {
        // Arrange - a blog id that is guaranteed not to exist
        var blogId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/blogs/{blogId}/publications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostPublication_RequiresAuthentication()
    {
        // Arrange
        var blogId = Guid.NewGuid();
        var publicationData = new
        {
            title = "Test Publication",
            text = "Test Content"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/v1/blogs/{blogId}/publications", publicationData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A notification names a publication the way every payload does — transliterated
    /// title, tilde, base64 of the guid — and the page a reader opens from one is the
    /// publication's own. The read route carried a <c>:guid</c> constraint, so that
    /// form never reached the model binder written for it: routing answered 404 with
    /// no body, which is the same status a missing publication answers and a different
    /// thing entirely. Both spellings must reach the handler and be refused by it.
    /// </summary>
    [Fact]
    public async Task GetPublication_AcceptsTheReadableGuidANotificationCarries()
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

    [Fact]
    public async Task DeletePublication_RequiresAuthentication()
    {
        // Arrange
        var publicationId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/publications/{publicationId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
