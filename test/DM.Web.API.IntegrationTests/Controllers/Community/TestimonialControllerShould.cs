using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Community;

/// <summary>
/// Integration tests for Testimonial (website reviews) controller
/// </summary>
public class TestimonialControllerShould : IntegrationTestBase
{
    public TestimonialControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static GeneralUser SeniorModerator() => new()
    {
        UserId = TestConstants.SeniorModeratorUserId,
        Username = TestConstants.SeniorModeratorUserUsername,
        Role = UserRole.SeniorModerator,
        AccessPolicy = AccessPolicy.NotSpecified
    };

    /// <summary>
    /// The name the answer is signed by, read out of the envelope.
    /// </summary>
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
    public async Task GetTestimonials_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/testimonials");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTestimonials_ReturnsPaginatedResults()
    {
        // Act
        var response = await Client.GetAsync("/v1/testimonials?number=1&size=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostTestimonial_RequiresAuthentication()
    {
        // Arrange
        var testimonialData = new
        {
            authorUsername = TestConstants.TestUserUsername,
            text = "Great website for roleplay!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/testimonials", testimonialData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A testimonial is posted ON BEHALF OF the participant the moderator
    /// names. Signed by the caller, the entry was a claim about the wrong
    /// person, on a page the whole site reads.
    /// </summary>
    [Fact]
    public async Task PostTestimonial_SignsItWithTheNamedParticipant()
    {
        // Arrange: a seeded user with no testimonial of their own yet.
        var request = WithBody(
            CreateAdminRequest(HttpMethod.Post, "/v1/testimonials"),
            new
            {
                authorUsername = TestConstants.InactiveUser1Username,
                text = "Отличная площадка для словесных ролевых игр!"
            });

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var author = await AuthorOf(response);
        author.Should().Be(TestConstants.InactiveUser1Username);
        author.Should().NotBe(TestConstants.AdminUserUsername);

        // Restore: the fixture is shared and this entry is not part of its seed.
        var id = await IdOf(response);
        var cleanup = await Client.SendAsync(
            CreateAdminRequest(HttpMethod.Delete, $"/v1/testimonials/{id}"));
        cleanup.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PostTestimonial_RefusesAnUnknownAuthor()
    {
        // Arrange
        var request = WithBody(
            CreateAdminRequest(HttpMethod.Post, "/v1/testimonials"),
            new { authorUsername = "nosuchparticipant", text = "Отличная площадка!" });

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostTestimonial_RefusesASecondEntryForTheSameParticipant()
    {
        // Arrange: the seeded testuser already has one.
        var request = WithBody(
            CreateAdminRequest(HttpMethod.Post, "/v1/testimonials"),
            new
            {
                authorUsername = TestConstants.TestUserUsername,
                text = "Второй отзыв того же автора"
            });

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostTestimonial_RefusesAnOrdinaryParticipant()
    {
        // Arrange
        var request = WithBody(
            CreateAuthenticatedRequest(HttpMethod.Post, "/v1/testimonials"),
            new
            {
                authorUsername = TestConstants.SecondUserUsername,
                text = "Отличная площадка!"
            });

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PatchTestimonial_RequiresAuthentication()
    {
        // Act
        var response = await Client.PatchAsJsonAsync(
            $"/v1/testimonials/{TestConstants.TestTestimonialId}",
            new { text = "Переписанный отзыв о сайте" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The right the inline edit on /about/testimonials is drawn from:
    /// WebsiteTestimonialIntention.Edit admits the named author.
    /// </summary>
    [Fact]
    public async Task PatchTestimonial_LetsTheNamedAuthorRewriteTheirOwn()
    {
        // Arrange: testuser is the seeded author of TestTestimonialId.
        var before = await Client.GetAsync($"/v1/testimonials/{TestConstants.TestTestimonialId}");
        var originalText = await TextOf(before);

        var request = WithBody(
            CreateAuthenticatedRequest(
                HttpMethod.Patch, $"/v1/testimonials/{TestConstants.TestTestimonialId}"),
            new { text = "Переписанный автором отзыв о сайте" });

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await TextOf(response)).Should().Be("Переписанный автором отзыв о сайте");
        // The signature does not move to whoever pressed save.
        (await AuthorOf(response)).Should().Be(TestConstants.TestUserUsername);

        // Restore the seeded text for the tests that read this list.
        var restore = WithBody(
            CreateAuthenticatedRequest(
                HttpMethod.Patch, $"/v1/testimonials/{TestConstants.TestTestimonialId}"),
            new { text = originalText });
        (await Client.SendAsync(restore)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PatchTestimonial_LetsSeniorModerationRewriteAnothers()
    {
        // Arrange
        var before = await Client.GetAsync($"/v1/testimonials/{TestConstants.SecondTestimonialId}");
        var originalText = await TextOf(before);

        var request = WithBody(
            CreateAuthenticatedRequest(
                HttpMethod.Patch,
                $"/v1/testimonials/{TestConstants.SecondTestimonialId}",
                SeniorModerator()),
            new { text = "Отредактировано старшей модерацией" });

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await AuthorOf(response)).Should().Be(TestConstants.SecondUserUsername);

        // Restore
        var restore = WithBody(
            CreateAuthenticatedRequest(
                HttpMethod.Patch,
                $"/v1/testimonials/{TestConstants.SecondTestimonialId}",
                SeniorModerator()),
            new { text = originalText });
        (await Client.SendAsync(restore)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A testimonial is signed. Below senior moderation there is nothing here
    /// but being the named author, and this caller is neither.
    /// </summary>
    [Fact]
    public async Task PatchTestimonial_RefusesAStranger()
    {
        // Arrange: an ordinary moderator, one rank below the override, on
        // somebody else's entry.
        var request = WithBody(
            CreateAuthenticatedRequest(
                HttpMethod.Patch,
                $"/v1/testimonials/{TestConstants.TestTestimonialId}",
                CustomWebApplicationFactory.CreateModeratorUser()),
            new { text = "Чужой отзыв переписан посторонним" });

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteTestimonial_RequiresAuthentication()
    {
        // Act
        var response = await Client.DeleteAsync($"/v1/testimonials/{TestConstants.TestTestimonialId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
