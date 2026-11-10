using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Community;

public class TestimonialControllerShould : IntegrationTestBase
{
    public TestimonialControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static async Task<string?> AuthorOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement
            .GetProperty("resource")
            .GetProperty("author")
            .GetProperty("username")
            .GetString();
    }

    private static async Task<string?> TextOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("resource").GetProperty("text").GetString();
    }

    private static async Task<string?> IdOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("resource").GetProperty("id").GetString();
    }

    private static HttpRequestMessage WithBody(HttpRequestMessage request, object body)
    {
        request.Content = JsonContent.Create(body);
        return request;
    }

    [Fact]
    public async Task GetTestimonialsReturnsOk()
    {
        var response = await Client.GetAsync("/v1/testimonials");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostTestimonialSignsItWithTheNamedParticipant()
    {
        // A seeded user with no testimonial of their own yet.
        var request = WithBody(
            CreateAdminRequest(HttpMethod.Post, "/v1/testimonials"),
            new
            {
                authorUsername = TestConstants.InactiveUser1Username,
                text = "Отличная площадка для словесных ролевых игр!"
            });

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var author = await AuthorOf(response);
        author.Should().Be(TestConstants.InactiveUser1Username);
        author.Should().NotBe(TestConstants.AdminUserLogin);

        // Restore: the fixture is shared and this entry is not part of its seed.
        var id = await IdOf(response);
        var cleanup = await Client.SendAsync(
            CreateAdminRequest(HttpMethod.Delete, $"/v1/testimonials/{id}"));
        cleanup.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PostTestimonialRefusesAnUnknownAuthor()
    {
        var request = WithBody(
            CreateAdminRequest(HttpMethod.Post, "/v1/testimonials"),
            new { authorUsername = "nosuchparticipant", text = "Отличная площадка!" });

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostTestimonialRefusesASecondEntryForTheSameParticipant()
    {
        // The seeded testuser already has one.
        var request = WithBody(
            CreateAdminRequest(HttpMethod.Post, "/v1/testimonials"),
            new
            {
                authorUsername = TestConstants.TestUserLogin,
                text = "Второй отзыв того же автора"
            });

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostTestimonialRefusesAnOrdinaryParticipant()
    {
        var request = WithBody(
            CreateAuthenticatedRequest(HttpMethod.Post, "/v1/testimonials"),
            new
            {
                authorUsername = TestConstants.SecondUserLogin,
                text = "Отличная площадка!"
            });

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // The right the inline edit on /about/testimonials is drawn from:
    // WebsiteTestimonialIntention.Edit admits the named author.
    [Fact]
    public async Task PatchTestimonialLetsTheNamedAuthorRewriteTheirOwn()
    {
        // Testuser is the seeded author of TestTestimonialId.
        var before = await Client.GetAsync($"/v1/testimonials/{TestConstants.TestTestimonialId}");
        var originalText = await TextOf(before);

        var request = WithBody(
            CreateAuthenticatedRequest(
                HttpMethod.Patch, $"/v1/testimonials/{TestConstants.TestTestimonialId}"),
            new { text = "Переписанный автором отзыв о сайте" });

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TextOf(response)).Should().Be("Переписанный автором отзыв о сайте");
        // The signature does not move to whoever pressed save.
        (await AuthorOf(response)).Should().Be(TestConstants.TestUserLogin);

        // Restore the seeded text for the tests that read this list.
        var restore = WithBody(
            CreateAuthenticatedRequest(
                HttpMethod.Patch, $"/v1/testimonials/{TestConstants.TestTestimonialId}"),
            new { text = originalText });
        (await Client.SendAsync(restore)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchTestimonialLetsSeniorModerationRewriteAnothers()
    {
        var before = await Client.GetAsync($"/v1/testimonials/{TestConstants.SecondTestimonialId}");
        var originalText = await TextOf(before);

        var request = WithBody(
            CreateAuthenticatedRequest(
                HttpMethod.Patch,
                $"/v1/testimonials/{TestConstants.SecondTestimonialId}",
                CustomWebApplicationFactory.CreateSeniorModeratorUser()),
            new { text = "Отредактировано старшей модерацией" });

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await AuthorOf(response)).Should().Be(TestConstants.SecondUserLogin);

        // Restore
        var restore = WithBody(
            CreateAuthenticatedRequest(
                HttpMethod.Patch,
                $"/v1/testimonials/{TestConstants.SecondTestimonialId}",
                CustomWebApplicationFactory.CreateSeniorModeratorUser()),
            new { text = originalText });
        (await Client.SendAsync(restore)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Below senior moderation there is nothing here but being the named author, and this caller is
    // neither.
    [Fact]
    public async Task PatchTestimonialRefusesAStranger()
    {
        // An ordinary moderator, one rank below the override, on
        // somebody else's entry.
        var request = WithBody(
            CreateAuthenticatedRequest(
                HttpMethod.Patch,
                $"/v1/testimonials/{TestConstants.TestTestimonialId}",
                CustomWebApplicationFactory.CreateModeratorUser()),
            new { text = "Чужой отзыв переписан посторонним" });

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
